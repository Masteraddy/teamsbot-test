using System.IO;

namespace APITest.Util
{
    public static class FileHelper
    {
        private static string directoryPath = Directory.GetCurrentDirectory() + "/transcriptss/";
        public static void CreateFile(string filePath)
        {
            var directoryExist = Directory.Exists(directoryPath);
            if (!directoryExist)
            {
                // Create the directory
                Console.WriteLine("Creating directory: " + directoryPath);
                Directory.CreateDirectory(directoryPath);
            }
            // Create the file
            Console.WriteLine("Creating file: " + directoryPath + filePath);
            File.Create(directoryPath + filePath);
        }

        public static void UpdateFile(string filePath, string content)
        {
            // Write the content to the file
            File.AppendAllText(directoryPath + filePath, content);
        }

        public static string ReadFile(string filePath)
        {
            // Read the content of the file
            return File.ReadAllText(directoryPath + filePath);
        }

        public static void DeleteFile(string filePath)
        {
            // Delete the file
            File.Delete(directoryPath + filePath);
        }

        public static bool FileExists(string filePath)
        {
            // Check if the file exists
            return File.Exists(directoryPath + filePath);
        }
    }
}