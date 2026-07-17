using System;
using System.Windows;
using Velopack;

namespace Kuro;

public partial class App : Application
{
    [STAThread]
    private static void Main(string[] args)
    {
        // This must be the first application code so installs and updates can complete cleanly.
        VelopackApp.Build().Run();

        App app = new();
        app.InitializeComponent();
        app.Run();
    }
}
