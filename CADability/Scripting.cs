using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections;
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
    /* !!! Scripting could work with .NET Standard 2.1 !!!
     * 
     */

    /// <summary>
    /// Evaluates formulas entered by the user (e.g. "2*sin(30)" or expressions that use named values) by compiling them
    /// with the C# compiler into a small in-memory assembly and executing it. It is used by the property entries that accept
    /// formulas; <see cref="GetDouble"/>, <see cref="GetGeoPoint"/> and <see cref="GetGeoVector"/> return the value of a
    /// formula in the context of a <see cref="NamedValuesProperty"/>.
    /// </summary>
    internal class Scripting
    {
        private ICodeCompiler compiler;
        private CompilerParameters compilerParams;
        public Scripting()
        {
            CodeDomProvider codeProvider = new CSharpCodeProvider();
            compiler = codeProvider.CreateCompiler();
            compilerParams = new CompilerParameters();
            compilerParams.CompilerOptions = "/target:library";
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");
            compilerParams.ReferencedAssemblies.Add("CADability.dll");
        }
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
            CompilerResults results = compiler.CompileAssemblyFromSource(compilerParams, code);
            if (results.Errors.Count > 0) throw new ScriptingException("CompileAssemblyFromSource error");
            Assembly generatedAssembly = results.CompiledAssembly;
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
