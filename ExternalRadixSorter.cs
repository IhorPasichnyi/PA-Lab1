// Gemini-generated version

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ExternalSortingAlgorithms
{
    public static class ExternalRadixSorter
    {
        // Ліміт пам'яті для обробки чанків (залишаємо запас для CLR та буферів файлових потоків під ліміт 150 МБ)
        private const long ChunkMemoryLimitBytes = 100 * 1024 * 1024; // 100 МБ
        private const int StreamBufferSize = 65536; // 64 КБ буфер I/O

        public static void Sort(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("Вхідний файл не знайдено.", inputFile);

            string tempDir = Path.Combine(Path.GetTempPath(), "RadixBuckets_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var bucketWriters = new Dictionary<char, StreamWriter>();

            try
            {
                // Крок 1: Розподіл елементів по тимчасових кишенях (Buckets) на основі символу-ключа
                using (StreamReader reader = new StreamReader(inputFile, Encoding.UTF8, false, StreamBufferSize))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        char key = line[0]; // Перший символ — ключ (латинська літера)

                        if (!bucketWriters.TryGetValue(key, out StreamWriter? writer))
                        {
                            string bucketPath = Path.Combine(tempDir, $"bucket_{(int)key}.tmp");
                            writer = new StreamWriter(bucketPath, true, Encoding.UTF8, StreamBufferSize);
                            bucketWriters[key] = writer;
                        }

                        writer.WriteLine(line);
                    }
                }

                // Закриваємо всі накопичувальні файли-кишені
                foreach (var writer in bucketWriters.Values)
                {
                    writer.Flush();
                    writer.Dispose();
                }

                // Крок 2: Збирання кишень у порядку спадання символів-ключа ('z'..'a', 'Z'..'A')
                var sortedKeys = bucketWriters.Keys
                    .OrderByDescending(c => c)
                    .ToList();

                using (StreamWriter outputWriter = new StreamWriter(outputFile, false, Encoding.UTF8, StreamBufferSize))
                {
                    foreach (char key in sortedKeys)
                    {
                        string bucketPath = Path.Combine(tempDir, $"bucket_{(int)key}.tmp");

                        if (File.Exists(bucketPath))
                        {
                            SortAndAppendBucket(bucketPath, outputWriter);
                            File.Delete(bucketPath);
                        }
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private static void SortAndAppendBucket(string bucketPath, StreamWriter outputWriter)
        {
            FileInfo info = new FileInfo(bucketPath);

            // Якщо кишеня вміщується в безпечний ліміт ОП, сортуємо повністю в пам'яті
            if (info.Length <= ChunkMemoryLimitBytes)
            {
                List<string> lines = File.ReadAllLines(bucketPath, Encoding.UTF8).ToList();
                lines.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal)); // За спаданням

                foreach (var line in lines)
                {
                    outputWriter.WriteLine(line);
                }
            }
            else
            {
                // Якщо одна кишеня перевищує ліміт пам'яті, сортуємо її чанками та зливаємо
                SortLargeBucketInChunks(bucketPath, outputWriter);
            }
        }

        private static void SortLargeBucketInChunks(string bucketPath, StreamWriter outputWriter)
        {
            List<string> chunkList = new List<string>();
            long currentChunkBytes = 0;
            List<string> tempChunkFiles = new List<string>();

            using (StreamReader reader = new StreamReader(bucketPath, Encoding.UTF8, false, StreamBufferSize))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    chunkList.Add(line);
                    currentChunkBytes += Encoding.UTF8.GetByteCount(line) + 2;

                    if (currentChunkBytes >= ChunkMemoryLimitBytes)
                    {
                        tempChunkFiles.Add(SaveSortedChunk(chunkList));
                        chunkList.Clear();
                        currentChunkBytes = 0;
                    }
                }

                if (chunkList.Count > 0)
                {
                    tempChunkFiles.Add(SaveSortedChunk(chunkList));
                    chunkList.Clear();
                }
            }

            MergeChunksToOutput(tempChunkFiles, outputWriter);

            foreach (var file in tempChunkFiles)
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        private static string SaveSortedChunk(List<string> chunk)
        {
            chunk.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));
            string tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, chunk, Encoding.UTF8);
            return tempFile;
        }

        private static void MergeChunksToOutput(List<string> chunkFiles, StreamWriter outputWriter)
        {
            var readers = chunkFiles.Select(f => new StreamReader(f, Encoding.UTF8, false, StreamBufferSize)).ToList();
            var priorityQueue = new PriorityQueue<(string line, int readerIndex), string>(
                Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.Ordinal)) // Max-Heap
            );

            try
            {
                for (int i = 0; i < readers.Count; i++)
                {
                    if (!readers[i].EndOfStream)
                    {
                        string? line = readers[i].ReadLine();
                        if (line != null)
                        {
                            priorityQueue.Enqueue((line, i), line);
                        }
                    }
                }

                while (priorityQueue.Count > 0)
                {
                    var (item, index) = priorityQueue.Dequeue();
                    outputWriter.WriteLine(item);

                    if (!readers[index].EndOfStream)
                    {
                        string? nextLine = readers[index].ReadLine();
                        if (nextLine != null)
                        {
                            priorityQueue.Enqueue((nextLine, index), nextLine);
                        }
                    }
                }
            }
            finally
            {
                foreach (var r in readers) r.Dispose();
            }
        }
    }
}