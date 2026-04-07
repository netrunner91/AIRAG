using System.ComponentModel;
using System.Globalization;
using Microsoft.SemanticKernel;
// --- TOOLS / PLUGINS ---

public class SalaryCalculatorPlugin
{
    [KernelFunction, Description("Calculates gross salary based on hours and hourly rate.")]
    public string Calculate(double hours, double rate)
        => (hours * rate).ToString("N2", CultureInfo.InvariantCulture) + " PLN";
}