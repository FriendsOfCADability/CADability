using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace CADability
{
    /// <summary>
    /// Counts the calls into the numeric minimizers and records where they come from.
    /// <para>
    /// The minimizers live in MathNet.Numerics, a third party assembly without sources, so a breakpoint
    /// cannot be set inside them - which makes it hard to find out whether an operation that has a closed
    /// form solution ends up in an iterative solver anyway, and from where. This records that at the
    /// boundary, on the CADability side, where the call is still attributable.
    /// </para>
    /// <para>
    /// Off unless <see cref="Enabled"/> is set, either directly or by the environment variable
    /// <c>CADABILITY_SOLVER_TRACE=1</c>. While off it costs one static field read per call. While on it walks
    /// the stack on every call, which is expensive - this is a diagnostic, not something to leave running.
    /// </para>
    /// </summary>
    internal static class SolverTrace
    {
        public static bool Enabled = Environment.GetEnvironmentVariable("CADABILITY_SOLVER_TRACE") == "1";

        /// <summary>How many callers above the recording site are kept. Enough to tell the paths apart,
        /// few enough that paths that only differ far up still aggregate into one line.</summary>
        public static int CallerDepth = 4;

        private static readonly ConcurrentDictionary<string, int> counts = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        private static int total;

        /// <summary>
        /// One call into a minimizer. <paramref name="solver"/> names the solver, the caller attributes
        /// (which the compiler fills in) name the site, and the stack above it names who wanted it.
        /// </summary>
        public static void Record(string solver,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            if (!Enabled) return;
            string key = solver + " @ " + System.IO.Path.GetFileName(file) + ":" + line +
                         " " + member + "\n              <- " + Callers();
            counts.AddOrUpdate(key, 1, (k, v) => v + 1);
            Interlocked.Increment(ref total);
        }

        private static string Callers()
        {
            // false: no file and line information. Resolving those would read the PDBs on every single call
            // and turn a slow diagnostic into an unusable one.
            StackTrace stack = new StackTrace(2, false);
            List<string> names = new List<string>();
            for (int i = 0; i < stack.FrameCount && names.Count < CallerDepth; i++)
            {
                MethodBase method = stack.GetFrame(i)?.GetMethod();
                if (method == null) continue;
                string type = method.DeclaringType?.Name ?? "?";
                if (type == nameof(SolverTrace)) continue;
                names.Add(type + "." + method.Name);
            }
            return names.Count > 0 ? string.Join(" <- ", names) : "(no managed caller)";
        }

        public static int TotalCalls => total;

        public static void Reset()
        {
            counts.Clear();
            Interlocked.Exchange(ref total, 0);
        }

        /// <summary>The recorded call paths, most frequent first.</summary>
        public static string Report(int top = 25)
        {
            if (total == 0)
                return Enabled
                    ? "solver trace: no minimizer was called"
                    : "solver trace: not enabled (set SolverTrace.Enabled or CADABILITY_SOLVER_TRACE=1)";

            StringBuilder result = new StringBuilder();
            result.AppendLine($"solver calls: {total} from {counts.Count} distinct call paths");
            result.AppendLine();
            foreach (KeyValuePair<string, int> entry in counts.OrderByDescending(e => e.Value).Take(top))
                result.AppendLine($"{entry.Value,8}  {entry.Key}");
            if (counts.Count > top) result.AppendLine($"          ... and {counts.Count - top} more paths");
            return result.ToString();
        }
    }
}
