using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace SpecCheck
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Loading file...");
            var document = XDocument.Load(args[0], LoadOptions.SetLineInfo);
            var enumerants = document.Element("signatures").Elements("api").Elements("enum").Elements("token").Select(
                x => new KeyValuePair<string, (int Val, int Line)>(x.Attribute("name").Value,
                    (int.Parse(x.Attribute("value").Value), ((IXmlLineInfo) x).LineNumber)));
            var enumNames = document.Element("signatures").Elements("api").Elements("enum")
                .Select(x => x.Attribute("name").Value);
            var definedTypes = document.Element("signatures").Elements("types").Elements("type")
                .Select(x => x.Attribute("name").Value).Concat(enumNames).ToList();
            var functions = document.Elements("signatures").Elements("api").Elements("function");
            Console.WriteLine("Checking enums...");
            Console.WriteLine();
            var totalErrors = 0;
            var errors = 0;
            var warnings = 0;
            var totalWarnings = 0;
            var enumerantDictionary = new Dictionary<string, (int, int)>();
            foreach (var enumerant in enumerants)
            {
                if (!enumerant.Key.StartsWith("GA_"))
                {
                    Console.WriteLine($"Error: Enumerant is missing prefix (line {enumerant.Value.Line})");
                    errors++;
                }
                
                if (enumerantDictionary.ContainsKey(enumerant.Key))
                {
                    if (enumerantDictionary[enumerant.Key].Item1 != enumerant.Value.Val)
                    {
                        Console.WriteLine("Error: Duplicate enumerant definition with conflicting values:");
                        Console.WriteLine(
                            $"    Already defined: {enumerant.Key} = {enumerantDictionary[enumerant.Key].Item1} (line {enumerantDictionary[enumerant.Key].Item2})");
                        Console.WriteLine(
                            $"    Duplicate: {enumerant.Key} = {enumerant.Value.Val} (line {enumerant.Value.Line})");
                        errors++;
                    }
                }
                else
                {
                    enumerantDictionary.Add(enumerant.Key, enumerant.Value);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{errors} errors and {warnings} warnings in enums.");
            totalErrors += errors;
            totalWarnings += warnings;
            Console.WriteLine();
            Console.WriteLine("Checking functions...");
            Console.WriteLine();
            errors = 0;
            warnings = 0;
            var functionNames = new Dictionary<string, int>();
            foreach (var function in functions)
            {
                var name = function.Attribute("name").Value;
                if (functionNames.ContainsKey(name))
                {
                    Console.WriteLine("Error: Duplicate function definition:");
                    Console.WriteLine($"    Already defined: {name} (line {functionNames[name]})");
                    Console.WriteLine($"    Duplicate: {name} (line {((IXmlLineInfo) function).LineNumber})");
                    errors++;
                }

                var parameterNames = new List<string>();
                foreach (var parameter in function.Elements("param"))
                {
                    var paramName = parameter.Attribute("name").Value;
                    var paramType = parameter.Attribute("type").Value;
                    if (parameterNames.Contains(paramName))
                    {
                        Console.WriteLine(
                            $"Error: Duplicate parameter definition (line {((IXmlLineInfo) parameter).LineNumber})");
                        errors++;
                    }
                    else
                    {
                        parameterNames.Add(paramName);
                    }

                    if (!definedTypes.Contains(paramType.TrimEnd('*')))
                    {
                        Console.WriteLine(
                            $"Error: Unknown parameter type {paramType} (line {((IXmlLineInfo) parameter).LineNumber})");
                        errors++;
                    }

                    if (paramName.ToLower() == "id")
                    {
                        Console.WriteLine($"Warning: Vague parameter name (id of what, exactly?) (line {((IXmlLineInfo) parameter).LineNumber})");
                        warnings++;
                    }

                    if (paramType.EndsWith("*") && parameter.Attribute("count") == null)
                    {
                        Console.WriteLine($"Warning: Pointer should have count (line {((IXmlLineInfo) parameter).LineNumber})");
                    }

                    if (paramType.EndsWith("*") && parameter.Attribute("flow") == null)
                    {
                        Console.WriteLine($"Warning: Pointer should have flow (line {((IXmlLineInfo) parameter).LineNumber})");
                    }
                }
                
                parameterNames.Clear();
            }

            Console.WriteLine();
            Console.WriteLine($"{errors} errors and {warnings} warnings in functions.");
            Console.WriteLine();
            totalErrors += errors;
            totalWarnings += warnings;
            Console.WriteLine($"{totalErrors} errors and {totalWarnings} warnings in {args[0]}");
            Console.WriteLine();
            Console.WriteLine(
                $"API Versions: {string.Join(", ", document.Element("signatures").Elements("api").Select(x => $"{x.Attribute("name").Value}-{x.Attribute("version").Value}"))}");
            Console.WriteLine();
            foreach (var api in document.Element("signatures").Elements("api"))
            {
                Console.WriteLine($"{api.Attribute("name").Value}-{api.Attribute("version").Value}:");
                Console.WriteLine();
                Console.WriteLine(
                    $"    Functions: {api.Elements("function").Count()} ({api.Elements("function").Count(x => x.Attribute("extension").Value.ToLower() != "core")} are extensions)");
                Console.WriteLine(
                    $"    Enumerants: {api.Elements("enum").Elements("token").Count()} ({api.Elements("enum").Where(x => x.Attribute("extension").Value.ToLower() != "core").SelectMany(x => x.Elements("token")).Count()} are extensions)");
                Console.WriteLine(
                    $"    Enums: {api.Elements("enum").Count()} ({api.Elements("enum").Count(x => x.Attribute("extension").Value.ToLower() != "core")} are extensions)");
                Console.WriteLine("    Extensions:");
                Console.WriteLine(
                    $"        {string.Join("\n        ", api.Elements("enum").Select(x => x.Attribute("extension").Value).Where(x => x.ToLower() != "core"))}");
            }
        }
    }
}