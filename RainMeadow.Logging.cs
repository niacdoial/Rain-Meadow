﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

#if !IS_SERVER  // only the client needs to depend on unity
using UnityEngine;
#endif

namespace RainMeadow
{
    public class InvalidProgrammerException : InvalidOperationException
    {
        public InvalidProgrammerException(string message) : base(message + " you goof") { }
    }

    public partial class RainMeadow
    {
#if IS_SERVER
        class LoggerClass {

            public void LogInfo(string msg) {
                Console.WriteLine("INFO : " + msg);
            }
            public void LogError(string msg) {
                Console.WriteLine("ERROR: " + msg);
            }
        }
        LoggerClass Logger;
        RainMeadow() {Logger = new LoggerClass();}
        static RainMeadow instance = new RainMeadow();

        static Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        private static string TrimCaller(string callerFile) {
            string cFile = callerFile.Substring(Math.Max(
                callerFile.LastIndexOf(Path.DirectorySeparatorChar),
                callerFile.LastIndexOf(Path.AltDirectorySeparatorChar)
            ) + 1);
            return cFile.Substring(0, cFile.LastIndexOf('.'));
        }
        private static string LogTime() {
#if IS_SERVER
            return Convert.ToUInt64(stopwatch.Elapsed.TotalMilliseconds).ToString();
#else
            return ((int)(Time.time * 1000)).ToString();
#endif
        }
        private static string LogDOT() { return DateTime.Now.ToUniversalTime().TimeOfDay.ToString().Substring(0, 8); }
        public static void Debug(object data, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerName = "")
        {
            instance.Logger.LogInfo($"{LogDOT()}|{LogTime()}|{TrimCaller(callerFile)}.{callerName}:{data}");
        }
        public static void DebugMe([CallerFilePath] string callerFile = "", [CallerMemberName] string callerName = "")
        {
            instance.Logger.LogInfo($"{LogDOT()}|{LogTime()}|{TrimCaller(callerFile)}.{callerName}");
        }
        public static void Error(object data, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerName = "")
        {
            instance.Logger.LogError($"{LogDOT()}|{LogTime()}|{TrimCaller(callerFile)}.{callerName}:{data}");
        }

        [Conditional("TRACING")]
        public static void Stacktrace()
        {
            var stacktrace = Environment.StackTrace;
            stacktrace = stacktrace.Substring(stacktrace.IndexOf('\n') + 1);
            stacktrace = stacktrace.Substring(stacktrace.IndexOf('\n'));
            instance.Logger.LogInfo(stacktrace);
        }

        [Conditional("TRACING")]
        public static void Dump(object data, [CallerFilePath] string callerFile = "", [CallerMemberName] string callerName = "")
        {
            var dump = JsonConvert.SerializeObject(data, Formatting.Indented, new JsonSerializerSettings
            {
                ContractResolver = ShallowJsonDump.customResolver,
                Converters = new List<JsonConverter>() { new ShallowJsonDump() }

            });
            instance.Logger.LogInfo($"{LogDOT()}|{LogTime()}|{TrimCaller(callerFile)}.{callerName}:{dump}");
        }

        // tracing stays on for one net-frame after pressing L
        public static bool tracing;
        // this better captures the caller member info for delegates/lambdas at the cost of using the stackframe
        [Conditional("TRACING")]
        public static void Trace(object data, [CallerFilePath] string callerFile = "")
        {
            if (tracing)
            {
            instance.Logger.LogInfo($"{LogDOT()}|{LogTime()}|{TrimCaller(callerFile)}.{new StackFrame(1, false).GetMethod()}:{data}");
            }
        }
    }
}
