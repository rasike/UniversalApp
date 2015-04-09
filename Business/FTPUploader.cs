#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using ReportBackupTool.Common;
using System.Reflection;

#endregion

namespace ReportBackupTool.Business
{
    public class FTPUploader
    {
        #region Constructors

        public FTPUploader()
        {
 
        }

        #endregion

        #region Create Folder In FTP

        /// <summary>
        /// Create a folder inside the given folder by given name.
        /// </summary>
        /// <param name="sourceFolder">Folder location inside which folder need to create.</param>
        /// <param name="newFolder">New folder name.</param>
        /// <returns></returns>
        public bool CreateDirectoryInFTP(string sourceFolder, string newFolder)
        {
            bool isSuccessfullyCreated = false;
            FtpWebResponse responseDir = null;
            Stream ftpStream = null;

            try
            {
                string staticPartOfURL = Constants.SnPFTP;
                string dynamicPartOfURL = sourceFolder;
                string fullURL = null;

                if (string.IsNullOrEmpty(dynamicPartOfURL))
                {
                    fullURL = staticPartOfURL;
                }
                else
                {
                    fullURL = Path.Combine(staticPartOfURL, dynamicPartOfURL);
                }

                if (string.IsNullOrEmpty(newFolder))
                {
                    //fullURL = fullURL;
                }
                else
                {
                    fullURL = Path.Combine(fullURL, newFolder);
                }

                string fullURLFinal = fullURL.Replace("\\", "/");

                RootServiceProvider.Logger.LogInfo(string.Format("Creating folder named : {0} inside the folder : {1}", newFolder, fullURLFinal), null, MethodBase.GetCurrentMethod().Name);

                FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(fullURLFinal));
                requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
                requestDir.Credentials = new NetworkCredential(Constants.SnPUserName, Constants.SnPPassword);
                requestDir.UsePassive = true;
                requestDir.UseBinary = true;
                requestDir.KeepAlive = false;
                responseDir = (FtpWebResponse)requestDir.GetResponse();
                ftpStream = responseDir.GetResponseStream();

                isSuccessfullyCreated = true;
            }
            catch (Exception ex)
            {
                isSuccessfullyCreated = false;
                RootServiceProvider.Logger.LogError(ex.Message, ex.StackTrace.ToString(), MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                if (ftpStream != null)
                {
                    ftpStream.Close();
                }

                if (responseDir != null)
                {
                    responseDir.Close();
                }
            }

            return isSuccessfullyCreated;
        }

        #endregion

        #region Check Folder Existance In FTP

        /// <summary>
        /// Check the availability of a given folder in a given location.
        /// </summary>
        /// <param name="containerFolderName">Source folder inside which search for given folder.</param>
        /// <param name="containFolder">Name of the folder of which check the existance.</param>
        public bool IsDirectoryExistsInFTPServer(string containerFolderName, string containFolder)
        {
            bool isFolderExists = false;
            FtpWebResponse response = null;
            StreamReader reader = null;

            try
            {
                string staticPartOfURL = Constants.SnPFTP;
                string dynamicPartOfURL = containerFolderName;
                string fullURL = null;

                if (string.IsNullOrEmpty(dynamicPartOfURL))
                {
                    fullURL = staticPartOfURL;
                }
                else
                {
                    fullURL = Path.Combine(staticPartOfURL, dynamicPartOfURL);
                }
               
                string fullURLFinal = fullURL.Replace("\\", "/");

                RootServiceProvider.Logger.LogInfo(string.Format("Searching file or folder named : {0} inside the folder : {1}",containFolder, fullURLFinal),null, MethodBase.GetCurrentMethod().Name);

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fullURLFinal);
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                request.Credentials = new NetworkCredential(Constants.SnPUserName, Constants.SnPPassword);
                response = (FtpWebResponse)request.GetResponse();

                Stream responseStream = response.GetResponseStream();
                reader = new StreamReader(responseStream);

                List<string> folderNames = new List<string>();

                while (reader.Peek() >= 0)
                {
                    string responseString = reader.ReadLine();
                    folderNames.Add(responseString);
                    if (responseString.Contains(containFolder))
                    {
                        isFolderExists = true;
                        break;
                    }

                    Console.WriteLine(responseString);
                }

                Console.WriteLine("Directory List Complete, status {0}", response.StatusDescription);
            }
            catch (Exception ex)
            {
                RootServiceProvider.Logger.LogError(ex.Message, ex.StackTrace.ToString(), MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }

                if (response != null)
                {
                    response.Close();
                }
            }

            return isFolderExists;
        }

        #endregion

        #region Upload File In FTP

        /// <summary>
        /// Upload given file to a given folder location.
        /// </summary>
        /// <param name="filePath">Full qualified name of the file path. Destination folder + Name of the file with extension in which name it should saved in FTP.</param>
        /// <param name="fileObj">Data of the object to upload to FTP.</param>
        public void UploadFileToFTP(string filePath, FileInfo fileObj)
        {
            FtpWebResponse response = null;

            try
            {
                string staticPartOfURL = Constants.SnPFTP;
                string dynamicPartOfURL = filePath;
                string fullURL = null;

                if (string.IsNullOrEmpty(dynamicPartOfURL))
                {
                    fullURL = staticPartOfURL;
                }
                else
                {
                    fullURL = Path.Combine(staticPartOfURL, dynamicPartOfURL);
                }

                string fullURLFinal = fullURL.Replace("\\", "/");

                RootServiceProvider.Logger.LogInfo(string.Format("Uploading file named : {0} to the folder : {1}", fileObj.Name, fullURLFinal), null, MethodBase.GetCurrentMethod().Name);

                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(fullURLFinal);
                request.Method = WebRequestMethods.Ftp.UploadFile;

                request.Credentials = new NetworkCredential(Constants.SnPUserName, Constants.SnPPassword);

                StreamReader sourceStream = null;
                Stream requestStream = null;

                try
                {
                    sourceStream = new StreamReader(fileObj.FullName);
                    byte[] fileContents = Encoding.UTF8.GetBytes(sourceStream.ReadToEnd());
                    sourceStream.Close();
                    request.ContentLength = fileContents.Length;

                    requestStream = request.GetRequestStream();
                    requestStream.Write(fileContents, 0, fileContents.Length);
                    requestStream.Close();
                }
                catch
                {
                    if (sourceStream != null)
                    {
                        sourceStream.Close();
                    }

                    if (requestStream != null)
                    {
                        requestStream.Close();
                    }
                }
                response = (FtpWebResponse)request.GetResponse();

                Console.WriteLine("Upload File Complete, status {0}", response.StatusDescription);
            }
            catch(Exception ex)
            {
                RootServiceProvider.Logger.LogError(ex.Message, ex.StackTrace.ToString(), MethodBase.GetCurrentMethod().Name);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }
        }

        #endregion
    }
}
