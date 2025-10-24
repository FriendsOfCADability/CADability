using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace CADability
{
    internal class ScriptingException : ApplicationException
    {
        public ScriptingException(string msg) : base(msg)
        {
        }
    }

    internal class Scripting
    {
        private object GetValue(NamedValuesProperty namedValues, string typename, string formula)
        {
            if (Settings.GlobalSettings.GetBoolValue("Scripting.ForceFloat", false))
            {
                formula = Regex.Replace(formula, @"(?<=/)(\d+)\b(?!\.)", "$1.0"); // macht aus "1/2" "1/2.0"
            }
            string code = @"
                using System;
                using System.Collections;
                using CADability;
                public class ScriptClass
                {
                    double sin(double d) { return Math.Sin(d); }
                    double cos(double d) { return Math.Cos(d); }
                    double tan(double d) { return Math.Tan(d); }
                    double Sin(double d) { return Math.Sin(d/180*Math.PI); }
                    double Cos(double d) { return Math.Cos(d/180*Math.PI); }
                    double Tan(double d) { return Math.Tan(d/180*Math.PI); }
                    GeoVector v(double x, double y, double z) { return new GeoVector(x,y,z); }
                    GeoPoint p(double x, double y, double z) { return new GeoPoint(x,y,z); }
                    %namedValues%
                    Hashtable namedValues;
                    public ScriptClass(Hashtable namedValues)
                    {
                        this.namedValues = namedValues;
                    }
                    public %type% Calculate()
                    {
                        return %formula%;
                    }
                }
                ";
            code = code.Replace("%formula%", formula);
            code = code.Replace("%type%", typename);
            code = code.Replace("%namedValues%", namedValues.GetCode());


            CSharpCompilation compilation = CSharpCompilation.Create(
                "out.dll",
                new SyntaxTree[] { SyntaxFactory.ParseSyntaxTree(Microsoft.CodeAnalysis.Text.SourceText.From(code, System.Text.Encoding.UTF8), CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp8)) },
                new MetadataReference[] {
                    MetadataReference.CreateFromFile(Assembly.Load("mscorlib").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Private.CoreLib").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("CADability").Location)},
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithOverflowChecks(true).WithOptimizationLevel(OptimizationLevel.Release));
            Assembly generatedAssembly = null;
            MemoryStream ms = new MemoryStream();
            Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);
            if (result.Success)
                generatedAssembly = Assembly.Load(ms.GetBuffer());
            else
                throw new ScriptingException("CompileAssemblyFromSource error");

            try
            {
                Module[] mods = generatedAssembly.GetModules(false);
                Type[] types = mods[0].GetTypes();
                foreach (Type type in types)
                {
                    if (type.Name == "ScriptClass")
                    {
                        ConstructorInfo ci = type.GetConstructor(new Type[] { typeof(Hashtable) });
                        object scriptClass = ci.Invoke(new object[] { namedValues.Table });
                        MethodInfo mi = type.GetMethod("Calculate");
                        if (mi != null)
                        {
                            try
                            {
                                return mi.Invoke(scriptClass, null);
                            }
                            catch (TargetInvocationException)
                            {
                                throw new ScriptingException("General error");
                            }
                        }
                    }
                }
            }
            catch (Exception e) // wenn hier irgendwas schief geht, dann nicht mehr asynchron laufen lassen
            {
                if (e is ThreadAbortException) throw (e);
                throw new ScriptingException("General error");
            }
            throw new ScriptingException("General error");
        }
        public GeoVector GetGeoVector(NamedValuesProperty namedValues, string formula)
        {
            return (GeoVector)GetValue(namedValues, "GeoVector", formula);
        }
        public GeoPoint GetGeoPoint(NamedValuesProperty namedValues, string formula)
        {
            return (GeoPoint)GetValue(namedValues, "GeoPoint", formula);
        }
        public double GetDouble(NamedValuesProperty namedValues, string formula)
        {
            return (double)GetValue(namedValues, "double", formula);
        }
    }
}
