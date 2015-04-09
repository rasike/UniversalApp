#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

#endregion

namespace ReportBackupTool.Common
{
    public static class Constants
    {
        public const string US = "US";
        public const string ASIAN = "ASIAN";
        public const string EN = "EN";
        public const string AR = "AR";
        public const string SUMMARY = "SUMMARY";

        private static string snpFTP = null;

        public static string SnPFTP
        {
            get
            {
                if (snpFTP == null)
                {
                    snpFTP = GetSnPFTP();
                }
                return snpFTP;
            }
        }

        private static string snpUserName = null;

        public static string SnPUserName
        {
            get
            {
                if (snpUserName == null)
                {
                    snpUserName = GetSnPUserName();
                }
                return snpUserName;
            }
        }

        private static string snpPassword = null;

        public static string SnPPassword
        {
            get
            {
                if (snpPassword == null)
                {
                    snpPassword = GetSnPPassword();
                }
                return snpPassword;
            }
        }

        #region Helper Methods

        public static string GetSnPReportPath()
        {
            string reportPath = null;
            try
            {
                //This need to be hardcoded path.
                string defaultPath = "\\ROOT\\SNP_XML";                
                reportPath = string.IsNullOrEmpty(ConfigurationManager.AppSettings["SNP_REPORT_PATH"]) ? defaultPath : ConfigurationManager.AppSettings["SNP_REPORT_PATH"];
            }
            catch
            {
 
            }

            return reportPath;
        }

        public static string GetSnPFTP()
        {
            string ftpPath = null;
            try
            {                
                string defaultPath = "ftp://59.163.254.77/SnP/";                
                ftpPath = string.IsNullOrEmpty(ConfigurationManager.AppSettings["SNP_FTP"]) ? defaultPath : ConfigurationManager.AppSettings["SNP_FTP"];
            }
            catch
            {

            }

            return ftpPath;
        }

        public static string GetSnPUserName()
        {
            string userName = null;
            try
            {
                string defaultUserName = "ldc-win-ftp";                
                userName = string.IsNullOrEmpty(ConfigurationManager.AppSettings["USER_NAME"]) ? defaultUserName : ConfigurationManager.AppSettings["USER_NAME"];
            }
            catch
            {

            }

            return userName;
        }

        public static string GetSnPPassword()
        {
            string password = null;
            try
            {
                string defaultPassword = "n60VU#WP";                
                password = string.IsNullOrEmpty(ConfigurationManager.AppSettings["PASSWORD"]) ? defaultPassword : ConfigurationManager.AppSettings["PASSWORD"];
            }
            catch
            {

            }

            return password;
        }

        #endregion
    }
}
