using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ozzytron;
using System;
using System.IO;

namespace Test.Ozzytron
{
    [TestClass]
    public class OpCodeDataSerializerTests
    {
        private string WriteRow(string[] values)
        {
            string output = "";
            for (var i = 0; i < values.Length; i++)
            {
                output += values[i];
                if (i != values.Length - 1)
                    output += ",";
            }
            output += Environment.NewLine;

            return output;
        }

        private string[] ReadRow(string line)
        {
            return line.Trim().Split(",");
        }

        [TestMethod]
        public void WriteOperationsToFile()
        {
            var a = new Bus();

            var output = "";
            foreach (var operation in a._cpu.opCodeLookup.Values)
            {
                if (output.Length == 0)
                {
                    // write header row
                    output += WriteRow(new string[] {
                        "Mnemonic",
                        "OpCode",
                        "Operate",
                        "Address",
                        "MinimumCycles",
                        "Size",
                    });
                }

                output += WriteRow(new string[] {
                        operation.Mnemonic,
                        "0x" + operation.OpCode.ToString("X2"),
                        operation.Operate.Method.Name,
                        operation.Address.Method.Name,
                        operation.MinimumCycles.ToString(),
                        operation.Size.ToString(),
                    });
            }

            File.WriteAllText("new-op-codes.csv", output);

            var newData = File.ReadAllText("new-op-codes.csv");
            var currentData = File.ReadAllText("Data\\op-codes.csv");

            Assert.AreEqual(currentData, newData);
        }

        [TestMethod]
        public void ReadOperationsFromFile()
        {
            var lines = File.ReadAllLines("Data\\op-codes.csv");

            var headers = new string[] { };
            var operations = "";
            foreach (var line in lines)
            {
                if (headers.Length == 0)
                {
                    headers = ReadRow(line);
                    continue;
                }

                var values = ReadRow(line);
                var opcode = "";
                var operation = "";
                for (var i = 0; i < headers.Length; i++)
                {
                    var header = headers[i];
                    var value = values[i];
                    if (header == "OpCode")
                        opcode = value;

                    if (header == "Mnemonic")
                        value = "\"" + value + "\"";

                    operation += header + "=" + value;
                    if (i < headers.Length - 1)
                        operation += ", ";
                }

                operations += "{ " + opcode + ", new Operation { " + operation + " } }," + Environment.NewLine;
            }

            Console.WriteLine(operations);
        }
    }

}
