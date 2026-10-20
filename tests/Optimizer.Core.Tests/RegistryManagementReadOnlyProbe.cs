using Optimizer.Core.Registry;
using Optimizer.Windows;

internal static class RegistryManagementReadOnlyProbe
{
    public static int Run()
    {
        var assessment = new WindowsRegistryManagementInspector()
            .Assess(RegistryPreferenceCatalog.FileExtensionsAddress);

        Console.WriteLine("Consulta local de gerenciamento do Registro — somente leitura.");
        Console.WriteLine($"Estado específico de HideFileExt: {assessment.Status}");
        Console.WriteLine(assessment.Detail);
        foreach (var item in assessment.EvidenceItems)
        {
            Console.WriteLine($"[{item.Kind}] {item.Status}");
            Console.WriteLine($"  Fonte: {item.Source}");
            Console.WriteLine($"  Alcance: {item.Scope}");
            Console.WriteLine($"  Resultado: {item.Detail}");
        }
        Console.WriteLine("Nenhum valor de Registro, política, ingresso ou componente do Windows foi alterado.");
        return 0;
    }
}
