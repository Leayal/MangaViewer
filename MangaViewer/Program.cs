using Microsoft.Win32;

namespace Leayal.MangaViewer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arg)
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            if (arg.Length != 0)
            {
                Application.Run(new Form1(arg[0]));
            }
            else
            {
                Application.Run(new Form1());
            }
        }

        /// <summary>Gets WebView2 Evergreen Runtime version and installation directory path if it's installed on the machine</summary>
        /// <returns>True if the runtime is installed. Otherwise, false.</returns>
        public static bool WebView2Version(out string directoryPath, out string version)
        {
            using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                if (hive is not null)
                {
                    using (var key = hive.OpenSubKey(Path.Combine("SOFTWARE", "Microsoft", "EdgeUpdate", "Clients", "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"), false))
                    {
                        if (key is not null)
                        {
                            if (key.GetValue("pv") is string str && key.GetValue("location") is string location)
                            {
                                directoryPath = location;
                                version = str;
                                return true;
                            }
                        }
                    }
                }
            }
            version = directoryPath = string.Empty;
            return false;
        }
    }
}