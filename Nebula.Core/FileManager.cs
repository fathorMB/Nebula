using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Core
{
    public class FileManager
    {
        private readonly string nodeDirectory;
        private readonly Dictionary<string, string> localFiles = new Dictionary<string, string>();

        public FileManager(int port)
        {
            nodeDirectory = CreateNodeDirectory(port);
        }

        private string CreateNodeDirectory(int port)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), $"Node_{port}_Files");
            Directory.CreateDirectory(path);
            return path;
        }

        public string AddFile(string filePath)
        {
            try
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(nodeDirectory, fileName);
                File.Copy(filePath, destPath, true);

                using var sha = SHA256.Create();
                using var stream = File.OpenRead(destPath);
                string fileId = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                localFiles[fileId] = destPath;
                return fileId;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("File add failed", ex);
            }
        }

        public bool TryGetFile(string fileId, out string filePath)
        {
            return localFiles.TryGetValue(fileId, out filePath);
        }

        public void SaveDownloadedFile(string fileId, Stream content)
        {
            string filePath = Path.Combine(nodeDirectory, fileId);
            using var fs = new FileStream(filePath, FileMode.Create);
            content.CopyTo(fs);
            localFiles[fileId] = filePath;
        }
    }
}
