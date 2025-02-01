using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Nebula.Core
{
    public class FileManager
    {
        private readonly string nodeDirectory;
        private readonly Dictionary<string, FileMetadata> fileMetadata = new Dictionary<string, FileMetadata>();

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

                string fileId = GenerateFileId(filePath);
                fileMetadata[fileId] = new FileMetadata
                {
                    FileId = fileId,
                    FileName = fileName,
                    UploadDate = DateTime.Now
                };

                return fileId;
            }
            catch (Exception ex)
            {
                Logger.LogError($"File add failed: {ex}");
                throw;
            }
        }

        private string GenerateFileId(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        public bool TryGetFile(string fileId, out string filePath)
        {
            if (fileMetadata.TryGetValue(fileId, out var metadata))
            {
                filePath = Path.Combine(nodeDirectory, metadata.FileName);
                return File.Exists(filePath);
            }
            filePath = null;
            return false;
        }

        public bool TryGetMetadata(string searchTerm, out FileMetadata metadata)
        {
            metadata = fileMetadata.Values.FirstOrDefault(m =>
                m.FileId.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                m.FileName.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0);

            return metadata != null;
        }

        public void SaveDownloadedFile(string fileId, string fileName, Stream content)
        {
            string filePath = Path.Combine(nodeDirectory, fileName);
            using var fs = new FileStream(filePath, FileMode.Create);
            content.CopyTo(fs);
            fileMetadata[fileId] = new FileMetadata
            {
                FileId = fileId,
                FileName = fileName,
                UploadDate = DateTime.Now
            };
        }
    }
}