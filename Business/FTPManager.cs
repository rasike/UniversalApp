#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ReportBackupTool.Common;
using System.Reflection;

#endregion

namespace ReportBackupTool.Business
{
    public class FTPManager
    {
        #region Fields

        FTPUploader ftpUploader = new FTPUploader();

        #endregion

        #region Constructors

        public FTPManager()
        {
 
        }

        #endregion

        #region Public Method

        public void ProcessFTPFile()
        {
            try
            {
                string todaysDateString = DateTime.Now.Day.ToString() + "_" + DateTime.Now.Month.ToString() + "_" + DateTime.Now.Year.ToString();

                Console.WriteLine(todaysDateString);
                Console.WriteLine("Creating Initial Folders for the Day");
                RootServiceProvider.Logger.LogInfo("Creating initial folders", null, MethodBase.GetCurrentMethod().Name);

                CreateInitialFoldersForDay(todaysDateString);

                RootServiceProvider.Logger.LogInfo("Initial folders were created successfully", null, MethodBase.GetCurrentMethod().Name);
                Console.WriteLine("Initial folders were created successfully");

                DirectoryInfo dirInfo = new DirectoryInfo(Constants.GetSnPReportPath());

                DirectoryInfo[] dirObjects = dirInfo.GetDirectories();

                for (int i = 0; i < dirObjects.Length; i++)
                {
                    DirectoryInfo directoryObj = dirObjects[i];

                    DirectoryInfo[] isinDirectories = directoryObj.GetDirectories();

                    for (int j = 0; j < isinDirectories.Length; j++)
                    {
                        DirectoryInfo usOrAsianDirectory = isinDirectories[j];

                        if (usOrAsianDirectory.Name == Constants.US || usOrAsianDirectory.Name == Constants.ASIAN)
                        {
                            DirectoryInfo[] summaryDirectories = usOrAsianDirectory.GetDirectories();

                            for (int k = 0; k < summaryDirectories.Length; k++)
                            {
                                DirectoryInfo summaryDirectory = summaryDirectories[k];

                                if (summaryDirectory.Name == Constants.SUMMARY)
                                {
                                    DirectoryInfo[] langDirectories = summaryDirectory.GetDirectories();

                                    for (int l = 0; l < langDirectories.Length; l++)
                                    {
                                        DirectoryInfo langDirectory = langDirectories[l];

                                        if (langDirectory.Name == Constants.EN || langDirectory.Name == Constants.AR)
                                        {
                                            FileInfo[] fileInfoArray = langDirectory.GetFiles("*.xml");

                                            foreach (FileInfo fileObj in fileInfoArray)
                                            {
                                                ProcessFileToUpateToFTP(fileObj, todaysDateString,directoryObj.Name, usOrAsianDirectory.Name, langDirectory.Name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                RootServiceProvider.Logger.LogError(ex.Message, ex.StackTrace.ToString(), MethodBase.GetCurrentMethod().Name); 
            }
        }

        #endregion

        #region Helper Methods

        private void ProcessFileToUpateToFTP(FileInfo fileObj, string todayDateString, string isin, string UsorAsian, string lang)
        {            
            string isinValue = Path.GetFileNameWithoutExtension(fileObj.Name.ToString());
            Console.WriteLine("ISIN : {0}", isin);

            RootServiceProvider.Logger.LogInfo(string.Format("Creating dynamic folders for datestring : {0} USorAsian : {1} ISIN : {2} Lang : {3}", todayDateString, UsorAsian, isinValue, lang), null, MethodBase.GetCurrentMethod().Name);
            Console.WriteLine("Creating dynamic folders for datestring : {0} USorAsian : {1} ISIN : {2} Lang : {3}", todayDateString, UsorAsian, isinValue, lang);

            CreateDynamicFolders(todayDateString, UsorAsian, isin, lang);

            Console.WriteLine("Dynamic folders were successfully created for datestring : {0} USorAsian : {1} ISIN : {2} Lang : {3}", todayDateString, UsorAsian, isinValue, lang);
            RootServiceProvider.Logger.LogInfo(string.Format("Dynamic folders were successfully created for datestring : {0} USorAsian : {1} ISIN : {2} Lang : {3}", todayDateString, UsorAsian, isinValue, lang), null, MethodBase.GetCurrentMethod().Name);

            string fileName = isinValue + ".xml";

            string filePath = BuildFileName(todayDateString, UsorAsian, isin, lang, fileName);

            Console.WriteLine("Uploading file to : {0}", filePath);
            RootServiceProvider.Logger.LogInfo(string.Format("Uploading file to : {0}", filePath), null, MethodBase.GetCurrentMethod().Name);

            ftpUploader.UploadFileToFTP(filePath, fileObj);

            Console.WriteLine("File uploaded successfully : {0}", filePath);
            RootServiceProvider.Logger.LogInfo(string.Format("File uploaded successfully : {0}", filePath), null, MethodBase.GetCurrentMethod().Name);
        }

        /// <summary>
        /// Creation of initially required folders. This folders required to create only once per day.
        /// </summary>
        /// <param name="todayDateString"></param>
        private void CreateInitialFoldersForDay(string todayDateString)
        {
            bool isTodaysFolderCreated = ftpUploader.IsDirectoryExistsInFTPServer(null, todayDateString);

            if (!isTodaysFolderCreated)
            {
                isTodaysFolderCreated = ftpUploader.CreateDirectoryInFTP(null, todayDateString);

                if (isTodaysFolderCreated)
                {
                    bool isUSFolderCreated = ftpUploader.CreateDirectoryInFTP(todayDateString, Constants.US);
                    bool isAsianFolderCreated = ftpUploader.CreateDirectoryInFTP(todayDateString, Constants.ASIAN);
                }
            }
            else
            {
                bool isUSFolderAvailable = ftpUploader.IsDirectoryExistsInFTPServer(todayDateString, Constants.US);
                if (!isUSFolderAvailable)
                {
                    isUSFolderAvailable = ftpUploader.CreateDirectoryInFTP(todayDateString, Constants.US);
                }

                bool isAsianFolderAvailable = ftpUploader.IsDirectoryExistsInFTPServer(todayDateString, Constants.ASIAN);
                if (!isAsianFolderAvailable)
                {
                    isAsianFolderAvailable = ftpUploader.CreateDirectoryInFTP(todayDateString, Constants.ASIAN);
                }
            }
        }

        /// <summary>
        /// Stock specific folders creation.
        /// </summary>
        /// <param name="todayDateString"></param>
        /// <param name="UsorAsian"></param>
        /// <param name="isin"></param>
        /// <param name="lang"></param>
        private void CreateDynamicFolders(string todayDateString, string UsorAsian, string isin, string lang)
        {
            string staticPartOfFolderPath = BuildFileName(todayDateString, UsorAsian, null, null, null);

            bool isDirectoryAavilable = ftpUploader.IsDirectoryExistsInFTPServer(staticPartOfFolderPath, isin);

            if (!isDirectoryAavilable)
            {
                isDirectoryAavilable = ftpUploader.CreateDirectoryInFTP(staticPartOfFolderPath, isin);

                string langFolderPath = BuildFileName(staticPartOfFolderPath, isin, null, null, null);

                if (isDirectoryAavilable)
                {
                    ftpUploader.CreateDirectoryInFTP(langFolderPath, lang);
                }
            }
            else
            {
                string langFolderPath = BuildFileName(staticPartOfFolderPath, isin, null, null, null);

                bool isLangDirAvailable = ftpUploader.IsDirectoryExistsInFTPServer(langFolderPath, lang);

                if (!isLangDirAvailable)
                {
                    ftpUploader.CreateDirectoryInFTP(langFolderPath, lang);
                }
            }
        }

        private static string BuildFileName(string arg1, string arg2, string arg3, string arg4, string arg5)
        {
            StringBuilder fileNameBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(arg1))
            {
                fileNameBuilder.Append(arg1);
                fileNameBuilder.Append(Path.DirectorySeparatorChar);
            }
            if (!string.IsNullOrEmpty(arg2))
            {
                fileNameBuilder.Append(arg2);
                fileNameBuilder.Append(Path.DirectorySeparatorChar);
            }
            if (!string.IsNullOrEmpty(arg3))
            {
                fileNameBuilder.Append(arg3);
                fileNameBuilder.Append(Path.DirectorySeparatorChar);
            }
            if (!string.IsNullOrEmpty(arg4))
            {
                fileNameBuilder.Append(arg4);
                fileNameBuilder.Append(Path.DirectorySeparatorChar);
            }
            if (!string.IsNullOrEmpty(arg5))
            {
                fileNameBuilder.Append(arg5);
                fileNameBuilder.Append(Path.DirectorySeparatorChar);
            }

            string filePath = fileNameBuilder.ToString();
            filePath = filePath.Remove(filePath.Length - 1);
            return filePath;
        }

        #endregion
    }
}
