using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Discord;

namespace GeistDesWaldes.Misc;

public static class GenericXmlSerializer
{
    public static async Task EnsurePathExistence<T>(LogHandler logger, string directoryPath, string filename = null, T file = default)
    {
        try
        {
            if (directoryPath == null)
                throw new ArgumentNullException(nameof(directoryPath));
            
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);

                if (logger != null)
                {
                    await logger.Log(new LogMessage(LogSeverity.Info, nameof(EnsurePathExistence), $"Created Directory: {directoryPath}"));
                }
            }

            if (filename != null && file != null)
            {
                string path = Path.Combine(directoryPath, $"{Path.GetFileNameWithoutExtension(filename)}.xml");

                if (!File.Exists(path))
                {
                    await SaveAsync<T>(logger, file, filename, directoryPath);

                    if (logger != null)
                    {
                        await logger.Log(new LogMessage(LogSeverity.Info, nameof(EnsurePathExistence), $"Created File: {path}"));
                    }
                }
            }
        }
        catch (Exception e)
        {
            if (logger != null)
            {
                await logger.Log(new LogMessage(LogSeverity.Error, nameof(EnsurePathExistence), $"Could not ensure path existence!\n{e}"));
            }
        }
    }

    public static async Task SaveAsync<T>(LogHandler logger, object objectToSave, string filename, string directoryPath)
    {
        try
        {
            T castedObject = (T)objectToSave;
            string path = Path.Combine(directoryPath, $"{Path.GetFileNameWithoutExtension(filename)}.xml");

            await using FileStream file = new(path, FileMode.Create, FileAccess.Write);
            await using XmlWriter writer = XmlWriter.Create(file, new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true,
                Async = true
            });

            XmlSerializer serializer = new(typeof(T));
            serializer.Serialize(writer, castedObject);

            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Verbose, nameof(SaveAsync), $"Successfully saved {path}!"));
        }
        catch (Exception e)
        {
            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Error, nameof(SaveAsync), $"Saving Failed!\n{e}"));
        }
    }

    public static async Task<T> LoadAsync<T>(LogHandler logger, string filename, string directoryPath)
    {
        T loadedFile = default;

        try
        {
            string path = Path.Combine(directoryPath, $"{Path.GetFileNameWithoutExtension(filename)}.xml");

            await using FileStream file = new(path, FileMode.OpenOrCreate, FileAccess.Read);
            using XmlReader reader = XmlReader.Create(file);

            XmlSerializer serializer = new(typeof(T));
            loadedFile = (T)serializer.Deserialize(reader);

            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Verbose, nameof(LoadAsync), $"Successfully loaded {path}!"));
        }
        catch (Exception e)
        {
            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Error, nameof(LoadAsync), $"Loading Failed!\n{e}"));
        }

        return loadedFile;
    }

    public static async Task<T[]> LoadAllAsync<T>(LogHandler logger, string directoryPath)
    {
        string[] files = Directory.GetFiles(directoryPath, "*.xml", SearchOption.TopDirectoryOnly);

        return await LoadAllAsync<T>(logger, files, directoryPath);
    }

    public static async Task<T[]> LoadAllAsync<T>(LogHandler logger, string[] fileNames, string directoryPath)
    {
        var loadedFiles = new List<T>();

        for (int i = 0; i < fileNames?.Length; i++)
        {
            try
            {
                FileInfo f = new(fileNames[i]);

                T loadedFile = await LoadAsync<T>(logger, f.Name, directoryPath);

                loadedFiles.Add(loadedFile);
            }
            catch (Exception e)
            {
                if (logger != null)
                    await logger.Log(new LogMessage(LogSeverity.Error, nameof(LoadAllAsync), $"Loading Failed!\n{e}"));
            }
        }

        return loadedFiles.ToArray();
    }


    public static async Task<bool> DeleteAsync(LogHandler logger, string filename, string directoryPath)
    {
        try
        {
            string path = Path.Combine(directoryPath, $"{Path.GetFileNameWithoutExtension(filename)}.xml");

            if (File.Exists(path))
            {
                File.Delete(path);

                if (logger != null)
                    await logger.Log(new LogMessage(LogSeverity.Info, nameof(DeleteAsync), $"Successfully deleted {path}!"));
            }
            else
            {
                if (logger != null)
                    await logger.Log(new LogMessage(LogSeverity.Info, nameof(DeleteAsync), $"File does not exist {path}!"));
            }

            return true;
        }
        catch (Exception e)
        {
            if (logger != null)
                await logger.Log(new LogMessage(LogSeverity.Error, nameof(DeleteAsync), $"Deleting Failed!\n{e}"));
        }

        return false;
    }
}