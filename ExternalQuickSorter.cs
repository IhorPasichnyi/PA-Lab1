using System;
using System.Collections.Generic;
using System.IO;

namespace ExternalSortingAlgorithms
{
    /// <summary>
    /// Зовнішнє швидке сортування (External QuickSort) файлу за спаданням.
    /// Задача B: рядок має вигляд "латинська літера (ключ) ... номер телефону".
    /// Ключ - перший символ рядка (латинські літери в ASCII = 1 байт).
    ///
    /// Ідея алгоритму:
    ///   1. Один прохід по файлу рахує, скільки разів зустрічається кожен ключ.
    ///   2. Якщо файл (або його частина) вміщується в ОП - сортуємо його в пам'яті
    ///      (Array.Sort = introsort, тобто модифікований quicksort).
    ///   3. Інакше обираємо опорні елементи (pivots) і розподіляємо рядки по тимчасових файлах:
    ///        [ключі > p0] [== p0] [між p0 і p1] [== p1] ... [ключі < p(m-1)]
    ///      Бакети "== pivot" уже відсортовані (всі ключі однакові) - їх просто копіюємо у результат.
    ///      Бакети "між pivot-ами" сортуємо рекурсивно.
    ///   4. Бакети обробляються в порядку спадання ключів, тому результат одразу пишеться
    ///      у вихідний файл послідовно, без окремого етапу злиття.
    ///
    /// Оскільки ключів дуже мало (26 або 52 літери), а значень багато, використовується
    /// розбиття на кілька частин за один прохід (PivotsPerPass) та окремі бакети для рівних ключів.
    /// Це гарантує завершення рекурсії навіть коли майже всі ключі однакові.
    /// </summary>
    public static class ExternalQuickSorter
    {
        private const int BufferSize = 65536;                    // буфер файлових потоків (64 КБ)
        private const int ReadBlockSize = 1024 * 1024;           // блок читання при розбитті (1 МБ)
        private const int MaxInMemoryBytes = 48 * 1024 * 1024;   // макс. розмір частини, яку сортуємо в ОП (48 МБ)
        private const int PivotsPerPass = 8;                     // кількість опорних ключів за один прохід
                                                                 // (1 = класичне 3-частинне розбиття)

        private delegate bool LineHandler(ReadOnlySpan<byte> line);

        public static void Sort(string inputFilePath, string outputFilePath)
        {
            if (string.Equals(Path.GetFullPath(inputFilePath), Path.GetFullPath(outputFilePath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Вхідний і вихідний файли мають бути різними.");

            string outputDir = Path.GetDirectoryName(Path.GetFullPath(outputFilePath))!;
            string tempDir = Path.Combine(outputDir, "ext_quick_tmp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                long[] keyCounts = CountKeys(inputFilePath);

                using var output = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
                SortPartition(inputFilePath, keyCounts, output, tempDir, deleteSource: false);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        // Сортує один файл-частину і дописує результат у кінець output.
        private static void SortPartition(string path, long[] keyCounts, Stream output, string tempDir, bool deleteSource)
        {
            int distinctKeys = 0;
            for (int k = 0; k < 256; k++)
            {
                if (keyCounts[k] > 0)
                    distinctKeys++;
            }

            if (distinctKeys == 0)
            {
                // порожня частина - нічого робити
            }
            else if (distinctKeys == 1)
            {
                // усі ключі однакові - частина вже "відсортована"
                AppendFile(path, output);
            }
            else if (new FileInfo(path).Length <= MaxInMemoryBytes)
            {
                SortInMemory(path, output);
            }
            else
            {
                PartitionAndRecurse(path, keyCounts, output, tempDir, deleteSource);
                return;
            }

            if (deleteSource)
                File.Delete(path);
        }

        // Розподіл рядків по бакетах за опорними ключами та рекурсивна обробка бакетів.
        private static void PartitionAndRecurse(string path, long[] keyCounts, Stream output, string tempDir, bool deleteSource)
        {
            int[] pivots = ChoosePivots(keyCounts);          // опорні ключі за спаданням
            int bucketCount = 2 * pivots.Length + 1;         // парні - діапазони, непарні - "== pivot"
            int[] bucketOf = BuildBucketTable(pivots);

            var bucketPaths = new string?[bucketCount];
            var writers = new FileStream?[bucketCount];
            var bucketKeyCounts = new long[]?[bucketCount];  // гістограми діапазонних бакетів

            try
            {
                ForEachLine(path, line =>
                {
                    byte key = KeyOf(line);
                    int b = bucketOf[key];

                    FileStream? w = writers[b];
                    if (w == null)
                    {
                        string bucketPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".tmp");
                        bucketPaths[b] = bucketPath;
                        w = new FileStream(bucketPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
                        writers[b] = w;

                        if ((b & 1) == 0)
                            bucketKeyCounts[b] = new long[256];
                    }

                    w.Write(line);
                    w.WriteByte((byte)'\n');

                    if ((b & 1) == 0)
                        bucketKeyCounts[b]![key]++;

                    return true;
                });
            }
            finally
            {
                foreach (FileStream? w in writers)
                    w?.Dispose();
            }

            // вихідна частина більше не потрібна - звільняємо місце на диску
            if (deleteSource)
                File.Delete(path);

            // бакети йдуть у порядку спадання ключів
            for (int b = 0; b < bucketCount; b++)
            {
                string? bucketPath = bucketPaths[b];
                if (bucketPath == null)
                    continue;

                if ((b & 1) == 1)
                {
                    // бакет "== pivot": усі ключі однакові
                    AppendFile(bucketPath, output);
                    File.Delete(bucketPath);
                }
                else
                {
                    SortPartition(bucketPath, bucketKeyCounts[b]!, output, tempDir, deleteSource: true);
                }
            }
        }

        // Вибір опорних ключів на основі точної гістограми (квантилі за кількістю рядків).
        private static int[] ChoosePivots(long[] keyCounts)
        {
            var keys = new List<int>();
            long total = 0;
            for (int k = 255; k >= 0; k--)
            {
                if (keyCounts[k] > 0)
                {
                    keys.Add(k);
                    total += keyCounts[k];
                }
            }

            // мало різних ключів - усі стають опорними, рекурсія не потрібна
            if (keys.Count <= PivotsPerPass)
                return keys.ToArray();

            var pivots = new List<int>();
            long step = Math.Max(1, total / (PivotsPerPass + 1));
            long cumulative = 0;

            foreach (int k in keys)
            {
                cumulative += keyCounts[k];
                if (pivots.Count < PivotsPerPass && cumulative >= step * (pivots.Count + 1))
                    pivots.Add(k);
            }

            return pivots.ToArray();
        }

        // Таблиця "значення ключа -> номер бакета".
        // Бакет 2*i   : ключі, більші за pivots[i], але менші за pivots[i-1]
        // Бакет 2*i+1 : ключі, рівні pivots[i]
        // Бакет 2*m   : ключі, менші за всі опорні
        private static int[] BuildBucketTable(int[] pivots)
        {
            var table = new int[256];
            for (int k = 0; k < 256; k++)
            {
                int bucket = 2 * pivots.Length;
                for (int i = 0; i < pivots.Length; i++)
                {
                    if (k == pivots[i]) { bucket = 2 * i + 1; break; }
                    if (k > pivots[i]) { bucket = 2 * i; break; }
                }
                table[k] = bucket;
            }
            return table;
        }

        // Сортування частини в оперативній пам'яті.
        // Зберігаємо лише байти файлу та масиви (початок, довжина, ключ+індекс) - це набагато
        // економніше, ніж масив string, і дозволяє сортувати long[] без делегата-компаратора.
        private static void SortInMemory(string path, Stream output)
        {
            byte[] data = File.ReadAllBytes(path);

            int lineCount = 0;
            int pos = 0;
            while (pos < data.Length)
            {
                int nl = data.AsSpan(pos).IndexOf((byte)'\n');
                lineCount++;
                if (nl < 0)
                    break;
                pos += nl + 1;
            }

            var starts = new int[lineCount];
            var lengths = new int[lineCount];
            var sortKeys = new long[lineCount];

            pos = 0;
            for (int i = 0; i < lineCount; i++)
            {
                int nl = data.AsSpan(pos).IndexOf((byte)'\n');
                int len = nl < 0 ? data.Length - pos : nl;

                starts[i] = pos;
                lengths[i] = len;

                byte key = len > 0 ? data[pos] : (byte)0;
                // (255 - key) у старших бітах -> сортування за зростанням дає спадання ключів;
                // індекс рядка в молодших бітах -> рівні ключі зберігають початковий порядок.
                sortKeys[i] = ((long)(255 - key) << 32) | (uint)i;

                pos += len + 1;
            }

            Array.Sort(sortKeys);

            for (int i = 0; i < lineCount; i++)
            {
                int idx = (int)(sortKeys[i] & 0xFFFFFFFFL);
                output.Write(data, starts[idx], lengths[idx]);
                output.WriteByte((byte)'\n');
            }
        }

        // Підрахунок гістограми ключів (один послідовний прохід по файлу).
        private static long[] CountKeys(string path)
        {
            var counts = new long[256];
            ForEachLine(path, line =>
            {
                counts[KeyOf(line)]++;
                return true;
            });
            return counts;
        }

        private static byte KeyOf(ReadOnlySpan<byte> line)
        {
            return line.Length > 0 ? line[0] : (byte)0;
        }

        // Дописує весь вміст файлу в кінець потоку.
        private static void AppendFile(string path, Stream output)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
            input.CopyTo(output, BufferSize);
        }

        // Послідовне читання рядків блоками по 1 МБ без створення string.
        // Рядок передається в handler без символу '\n'. Якщо handler повертає false - читання зупиняється.
        private static void ForEachLine(string path, LineHandler handler)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);

            byte[] block = new byte[ReadBlockSize];
            int carry = 0;   // невикористаний залишок (неповний рядок) на початку блока
            int read;

            while ((read = input.Read(block, carry, block.Length - carry)) > 0)
            {
                int end = carry + read;
                int start = 0;

                while (true)
                {
                    int nl = block.AsSpan(start, end - start).IndexOf((byte)'\n');
                    if (nl < 0)
                        break;

                    if (!handler(block.AsSpan(start, nl)))
                        return;

                    start += nl + 1;
                }

                carry = end - start;
                if (carry == block.Length)
                    throw new InvalidDataException("Рядок довший за буфер читання.");

                if (carry > 0)
                    Buffer.BlockCopy(block, start, block, 0, carry);
            }

            // останній рядок без завершального '\n'
            if (carry > 0)
                handler(block.AsSpan(0, carry));
        }
    }
}
