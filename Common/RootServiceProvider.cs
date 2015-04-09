using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReportBackupTool.Loggers;
using System.Windows.Forms;

namespace ReportBackupTool.Common
{
    public static class RootServiceProvider
    {
       // public static IExceptionHandler ExceptionHandler;

        public static ILogger Logger;        

        public static bool IsProductionVersion = false;

        public static IConsoleLogger ConsoleLogger;

        public static Form ShellForm;

    }

    public delegate void MethodInvoker();
}
