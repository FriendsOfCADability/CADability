using CADability.GeoObject;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Path = System.IO.Path;

namespace CADability.BooleanSweep
{
    /// <summary>
    /// Runs boolean operations of solids in near-coincident positions (issue #400) and compares the volumes of the results with
    /// the exact ones. Every case runs in a process of its own, so a stack overflow or an endless loop costs only that case.
    /// See README.md.
    /// </summary>
    internal static class Program
    {
        const string ResultTag = "SWEEP-RESULT\t";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Columns = { "id", "family", "variant", "offset", "operation", "status", "solids", "openEdges", "volume", "integrated", "expected", "tolerance", "ms", "message" };

        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "run") return RunOne(args[1]);
            if (args.Length >= 1 && args[0] == "list")
            {
                foreach (SweepCase c in Cases.All()) Console.WriteLine(c.Id);
                return 0;
            }
            return Sweep(args);
        }

        /// <summary>Runs one case in this process and writes its result as one tab separated line.</summary>
        static int RunOne(string id)
        {
            SweepCase? c = Cases.All().FirstOrDefault(x => x.Id == id);
            if (c == null)
            {
                Console.Error.WriteLine("unknown case " + id);
                return 2;
            }
            Dictionary<string, string> r = new Dictionary<string, string>();
            Stopwatch sw = Stopwatch.StartNew();
            double expected = c.Expected(c.Offset), tolerance = c.Tolerance;
            r["expected"] = expected.ToString("R", Inv);
            r["tolerance"] = tolerance.ToString("R", Inv);
            try
            {
                Solid a = c.Variant.A(c.Offset), b = c.Variant.B(c.Offset);
                Solid[]? res = c.Operation switch
                {
                    Operation.AMinusB => Solid.Subtract(a, b),
                    Operation.BMinusA => Solid.Subtract(b, a),
                    Operation.Intersect => Solid.Intersect(a, b),
                    _ => Union(a, b),
                };
                sw.Stop();
                int solids = res?.Length ?? 0;
                int open = res?.Sum(s => s.Shells.Sum(sh => sh.OpenEdgesExceptPoles.Length)) ?? 0;
                // the volume from a fine triangulation; ShellMetrics.IntegratedVolume is up to about 1e-4 relative off for faces
                // bounded by intersection curves, it is only written for comparison: a large difference shows a broken result
                double volume = res?.Sum(s => s.Volume(1e-4)) ?? 0.0;
                double integrated = res?.Sum(s => s.Shells.Sum(sh => ShellMetrics.IntegratedVolume(sh))) ?? 0.0;
                r["integrated"] = integrated.ToString("R", Inv);
                r["solids"] = res == null ? "null" : solids.ToString(Inv);
                r["openEdges"] = open.ToString(Inv);
                r["volume"] = volume.ToString("R", Inv);
                string status;
                if (solids == 0)
                {
                    if (expected <= tolerance) status = "OK";
                    // Solid.Unite returns null for disjoint solids and for solids that touch only in a line or a point (no manifold)
                    else if (c.Operation == Operation.Union && (c.Offset > 0.0 || (c.Offset == 0.0 && c.Variant.LineOrPointContact))) status = "LIMITATION";
                    else status = "EMPTY";
                }
                else if (open > 0) status = "OPEN";
                // a solid touching the inside of another one cannot be subtracted, the first one is returned unchanged (#449)
                else if (c.Operation == Operation.AMinusB && c.Variant.BInsideA && c.Offset >= 0.0 && c.Offset <= 1e-5 &&
                    Math.Abs(volume - c.Variant.VolumeA(c.Offset)) <= tolerance) status = "LIMITATION";
                else if (Math.Abs(volume - expected) > tolerance) status = "VOLUME";
                else status = "OK";
                r["status"] = status;
            }
            catch (Exception e)
            {
                sw.Stop();
                r["status"] = "EXCEPTION";
                r["message"] = e.GetType().Name + ": " + e.Message;
            }
            r["ms"] = sw.ElapsedMilliseconds.ToString(Inv);
            Console.WriteLine(ResultTag + string.Join("\t", new[] { "solids", "openEdges", "volume", "integrated", "expected", "tolerance", "ms", "status", "message" }
                .Select(k => k + "=" + Clean(r.TryGetValue(k, out string? v) ? v : ""))));
            return 0;
        }

        /// <summary><see cref="Solid.Unite"/> returns one solid or null; disjoint solids are their own union.</summary>
        static Solid[]? Union(Solid a, Solid b)
        {
            Solid u = Solid.Unite(a, b);
            if (u != null) return new[] { u };
            return null;
        }

        static string Clean(string s) => s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        /// <summary>
        /// Runs all cases (or those whose id contains the --filter text) in child processes and writes sweep.csv and summary.md
        /// into the --out directory. With --baseline &lt;csv&gt; the summary lists the cases whose status changed.
        /// </summary>
        static int Sweep(string[] args)
        {
            string outDir = Arg(args, "--out") ?? "sweep-results";
            string? filter = Arg(args, "--filter");
            string? baseline = Arg(args, "--baseline");
            int parallel = int.Parse(Arg(args, "--parallel") ?? Environment.ProcessorCount.ToString(Inv), Inv);
            int timeout = int.Parse(Arg(args, "--timeout") ?? "120", Inv);
            Directory.CreateDirectory(outDir);

            List<SweepCase> cases = Cases.All().Where(c => filter == null || c.Id.Contains(filter)).ToList();
            string self = typeof(Program).Assembly.Location;
            Dictionary<string, string>[] results = new Dictionary<string, string>[cases.Count];
            int done = 0;
            Stopwatch total = Stopwatch.StartNew();
            Parallel.For(0, cases.Count, new ParallelOptions { MaxDegreeOfParallelism = parallel }, i =>
            {
                results[i] = RunChild(self, cases[i], timeout);
                int n = Interlocked.Increment(ref done);
                if (n % 25 == 0 || n == cases.Count) Console.WriteLine($"{n}/{cases.Count} ({total.Elapsed.TotalSeconds:F0} s)");
            });

            StringBuilder csv = new StringBuilder();
            csv.AppendLine(string.Join(";", Columns));
            foreach (Dictionary<string, string> r in results) csv.AppendLine(string.Join(";", Columns.Select(k => r.TryGetValue(k, out string? v) ? v.Replace(';', ',') : "")));
            File.WriteAllText(Path.Combine(outDir, "sweep.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(outDir, "summary.md"), Summary(results, baseline, total.Elapsed));
            Console.WriteLine($"written to {Path.GetFullPath(outDir)}");
            return 0;
        }

        static Dictionary<string, string> RunChild(string self, SweepCase c, int timeoutSeconds)
        {
            Dictionary<string, string> r = new Dictionary<string, string>
            {
                ["id"] = c.Id, ["family"] = c.Variant.Family, ["variant"] = c.Variant.Name,
                ["offset"] = c.Offset.ToString("R", Inv), ["operation"] = c.Operation.ToString(),
            };
            ProcessStartInfo psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add(self);
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add(c.Id);
            using Process p = Process.Start(psi)!;
            Task<string> stdout = p.StandardOutput.ReadToEndAsync();
            Task<string> stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutSeconds * 1000))
            {
                try { p.Kill(true); } catch { }
                r["status"] = "TIMEOUT";
                r["ms"] = (timeoutSeconds * 1000).ToString(Inv);
                return r;
            }
            string line = stdout.Result.Split('\n').FirstOrDefault(l => l.StartsWith(ResultTag)) ?? "";
            if (line.Length == 0)
            {
                r["status"] = "CRASH";
                string err = stderr.Result + stdout.Result;
                r["message"] = Clean(err.Length > 300 ? err.Substring(0, 300) : err) + $" (exit code {p.ExitCode})";
                return r;
            }
            foreach (string kv in line.Substring(ResultTag.Length).TrimEnd('\r').Split('\t'))
            {
                int eq = kv.IndexOf('=');
                if (eq > 0) r[kv.Substring(0, eq)] = kv.Substring(eq + 1);
            }
            return r;
        }

        static string Summary(Dictionary<string, string>[] results, string? baselineFile, TimeSpan elapsed)
        {
            StringBuilder s = new StringBuilder();
            s.AppendLine("# Boolean sweep");
            s.AppendLine();
            s.AppendLine($"{results.Length} cases, {elapsed.TotalSeconds:F0} s.");
            s.AppendLine();
            string[] statuses = { "OK", "VOLUME", "EMPTY", "OPEN", "EXCEPTION", "TIMEOUT", "CRASH", "LIMITATION" };
            s.AppendLine("| status | cases |");
            s.AppendLine("|---|---|");
            foreach (var g in results.GroupBy(r => r["status"]).OrderBy(g => Array.IndexOf(statuses, g.Key)))
                s.AppendLine($"| {g.Key} | {g.Count()} |");
            s.AppendLine();
            s.AppendLine("## By variant (cases that are not OK)");
            s.AppendLine();
            s.AppendLine("| family / variant | not OK | of | statuses |");
            s.AppendLine("|---|---|---|---|");
            foreach (var g in results.GroupBy(r => r["family"] + " / " + r["variant"]))
            {
                var bad = g.Where(r => r["status"] != "OK" && r["status"] != "LIMITATION").ToList();
                string st = string.Join(", ", bad.GroupBy(r => r["status"]).Select(x => $"{x.Key} {x.Count()}"));
                s.AppendLine($"| {g.Key} | {bad.Count} | {g.Count()} | {st} |");
            }
            s.AppendLine();
            s.AppendLine("## By offset (cases that are not OK)");
            s.AppendLine();
            s.AppendLine("| offset | not OK | of |");
            s.AppendLine("|---|---|---|");
            foreach (var g in results.GroupBy(r => r["offset"]).OrderBy(g => double.Parse(g.Key, Inv)))
                s.AppendLine($"| {g.Key} | {g.Count(r => r["status"] != "OK" && r["status"] != "LIMITATION")} | {g.Count()} |");
            if (baselineFile != null && File.Exists(baselineFile))
            {
                Dictionary<string, string> before = File.ReadAllLines(baselineFile).Skip(1).Select(l => l.Split(';'))
                    .Where(f => f.Length > 5).ToDictionary(f => f[0], f => f[5]);
                var changed = results.Where(r => before.TryGetValue(r["id"], out string? b) && b != r["status"]).ToList();
                s.AppendLine();
                s.AppendLine($"## Changes against {Path.GetFileName(baselineFile)}");
                s.AppendLine();
                s.AppendLine($"{changed.Count(r => r["status"] == "OK")} fixed, {changed.Count(r => before[r["id"]] == "OK")} broken, {changed.Count(r => r["status"] != "OK" && before[r["id"]] != "OK")} changed from one failure to another.");
                s.AppendLine();
                foreach (var r in changed) s.AppendLine($"- `{r["id"]}`: {before[r["id"]]} → {r["status"]}");
            }
            s.AppendLine();
            s.AppendLine("## Cases that are not OK");
            s.AppendLine();
            foreach (var r in results.Where(r => r["status"] != "OK"))
            {
                string Get(string k) => r.TryGetValue(k, out string? v) ? v : "";
                s.AppendLine($"- `{r["id"]}`: {r["status"]}, solids {Get("solids")}, open edges {Get("openEdges")}, volume {Get("volume")}, expected {Get("expected")} ± {Get("tolerance")} {Get("message")}");
            }
            return s.ToString();
        }

        static string? Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
