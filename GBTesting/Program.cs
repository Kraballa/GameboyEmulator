using GB.emu;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GBTesting
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("emulator testing");

            TestCPU CPU = new TestCPU(new Rom("cpu_instrs.gb"), Flags.ZERO);
            CPU.ReportOpcodes = true;
            CPU.Run();
        }

        private static void RunTests()
        {
            MethodInfo[] setups = GetMethodsOfAttribute(typeof(SetupAttribute));
            MethodInfo[] tests = GetMethodsOfAttribute(typeof(TestAttribute));
            MethodInfo[] cleanups = GetMethodsOfAttribute(typeof(CleanupAttribute));

            foreach (MethodInfo method in tests)
            {
                foreach (MethodInfo setup in setups)
                {
                    setup.Invoke(null, null);
                }
                method.Invoke(null, null);
                foreach (MethodInfo cleanup in cleanups)
                {
                    cleanup.Invoke(null, null);
                }
            }
        }

        private static MethodInfo[] GetMethodsOfAttribute(Type attrType)
        {
            string assemblyName = "GBTesting";
            byte[] assemblyBytes = File.ReadAllBytes(assemblyName);
            Assembly assembly = Assembly.Load(assemblyBytes);

            return assembly.GetTypes()
                      .SelectMany(t => t.GetMethods())
                      .Where(m => m.GetCustomAttributes(attrType, false).Length > 0)
                      .ToArray();
        }
    }
}
