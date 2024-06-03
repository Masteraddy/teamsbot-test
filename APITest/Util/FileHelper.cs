using System.IO;

namespace APITest.Util
{
    public static class FileHelper
    {
        private static string directoryPath = Directory.GetCurrentDirectory() + "/transcriptss/";
        public static void CreateFile(string filePath)
        {
            try
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
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
        }

        public static void UpdateFile(string filePath, string content)
        {
            try
            {
                // Write the content to the file
                File.AppendAllText(directoryPath + filePath, content);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
        }

        public static string ReadFile(string filePath)
        {
            try
            {
                // Read the content of the file
                return File.ReadAllText(directoryPath + filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
                return null;
            }
        }

        public static void DeleteFile(string filePath)
        {
            try
            {
                // Delete the file
                File.Delete(directoryPath + filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
        }

        public static bool FileExists(string filePath)
        {
            try
            {
                // Check if the file exists
                return File.Exists(directoryPath + filePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
                return false;
            }
        }
    }
}