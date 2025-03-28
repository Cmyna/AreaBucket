using Colossal.Json;
using Colossal.Mathematics;
using Colossal.PSI.Environment;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Collections;
using Unity.Mathematics;

namespace AreaBucket.Utils
{
    internal static class DumpUtils
    {

        public static void WriteSegments(NativeList<Line2.Segment> lines)
        {
            try
            {
                var directoryPath = Path.Combine(EnvPath.kUserDataPath, "ModsData", "AreaBucket", "dumps");
                Mod.Logger.Info($"dump dir path: {directoryPath}");

                if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath); 

                var files = Directory.GetFiles(directoryPath);
                Regex regex = new Regex(@"lines-(\d+)\.json");

                var numbers = files.Select(file => {
                    Match match = regex.Match(Path.GetFileName(file));
                    return match.Success ? int.Parse(match.Groups[1].Value) : (int?)null;
                }).Where(num => num.HasValue).Select(num => num.Value);

                int maxNumber = numbers.Any() ? (numbers.Max() + 1) : 0;

                var newFileName = $"lines-{maxNumber}.json";
                var newFilePath = Path.Combine(directoryPath, newFileName);

                WriteSegments(lines, newFilePath);
            } catch (Exception ex)
            {
                Mod.Logger.Error($"{ex.Message} {ex.StackTrace}");
            }


        }

        public static void WriteSegments(NativeList<Line2.Segment> lines, string filePath)
        {
            float4[] data = lines.AsArray().ToArray().Select((Line2.Segment line) => line.ab ).ToArray();
            var str = JSON.Dump(data);

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(filePath, str);
            }
            catch (Exception ex)
            {
                Mod.Logger.Error(ex.Message);
            }
        }


        public static string WithAssemblyPath(string fileName)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string directoryPath = Path.GetDirectoryName(assemblyPath);
            return Path.Combine(directoryPath, "dumps", fileName);
        }
    }


}
