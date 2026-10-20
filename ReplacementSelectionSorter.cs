// ChatGPT-generated version

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ExternalSortingAlgorithms
{
    public sealed class Record
    {
        public char Key { get; }
        public string Text { get; }
        public string Phone { get; }

        public Record(char key, string text, string phone)
        {
            Key = key;
            Text = text;
            Phone = phone;
        }

        public override string ToString()
        {
            return $"{Key}-{Text}-{Phone}";
        }
    }

    public sealed class RecordKeyComparer : IComparer<char>
    {
        // PriorityQueue removes the smallest priority first.
        // Reverse comparison makes the largest letter come first.
        public int Compare(char x, char y)
        {
            return char.ToUpperInvariant(y)
                .CompareTo(char.ToUpperInvariant(x));
        }
    }

    public static class ReplacementSelectionSorter
    {
        private const int IoBufferSize = 65536;

        // Number of records held in memory during run generation.
        private const int HeapCapacity = 100_000;

        // Maximum number of runs opened simultaneously during merging.
        private const int MergeFanIn = 32;

        private static readonly Encoding Utf8 =
            new UTF8Encoding(false);

        private static int CompareKeys(char x, char y)
        {
            return char.ToUpperInvariant(x)
                .CompareTo(char.ToUpperInvariant(y));
        }

        private static Record ParseRecord(string line)
        {
            string[] fields = line.Split('-');

            if (fields.Length != 3 ||
                fields[0].Length != 1 ||
                !IsLatinLetter(fields[0][0]))
            {
                throw new FormatException(
                    $"Invalid record: {line}");
            }

            if (fields[1].Length > 45)
            {
                throw new FormatException(
                    $"Text exceeds 45 characters: {line}");
            }

            if (string.IsNullOrWhiteSpace(fields[2]))
            {
                throw new FormatException(
                    $"Phone number is empty: {line}");
            }

            return new Record(
                fields[0][0],
                fields[1],
                fields[2]);
        }

        private static bool IsLatinLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') ||
                   (c >= 'a' && c <= 'z');
        }

        // Max-heap: the largest key is at index 0.
        private static void HeapPush(
            List<Record> heap, Record item)
        {
            int i = heap.Count;
            heap.Add(item);

            while (i > 0)
            {
                int parent = (i - 1) / 2;

                if (CompareKeys(
                    heap[parent].Key, item.Key) >= 0)
                {
                    break;
                }

                heap[i] = heap[parent];
                i = parent;
            }

            heap[i] = item;
        }

        private static Record HeapPop(List<Record> heap)
        {
            Record result = heap[0];
            Record last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);

            if (heap.Count == 0)
                return result;

            int i = 0;

            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;

                if (left >= heap.Count)
                    break;

                int largest = left;

                if (right < heap.Count &&
                    CompareKeys(
                        heap[right].Key,
                        heap[left].Key) > 0)
                {
                    largest = right;
                }

                if (CompareKeys(
                    last.Key, heap[largest].Key) >= 0)
                {
                    break;
                }

                heap[i] = heap[largest];
                i = largest;
            }

            heap[i] = last;
            return result;
        }

        private static StreamReader OpenReader(string path)
        {
            return new StreamReader(
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    IoBufferSize,
                    FileOptions.SequentialScan),
                Utf8);
        }

        private static StreamWriter OpenWriter(string path)
        {
            return new StreamWriter(
                new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    IoBufferSize,
                    FileOptions.SequentialScan),
                Utf8);
        }

        // Phase 1: generate sorted runs using Replacement Selection.
        private static List<string> CreateRuns(
            string inputPath, string tempDirectory)
        {
            var runs = new List<string>();
            var active = new List<Record>(HeapCapacity);
            var frozen = new List<Record>(HeapCapacity);

            using StreamReader reader = OpenReader(inputPath);

            // Fill the initial heap.
            while (active.Count < HeapCapacity)
            {
                string? line = reader.ReadLine();

                if (line == null)
                    break;

                HeapPush(active, ParseRecord(line));
            }

            int runNumber = 0;

            while (active.Count > 0)
            {
                string runPath = Path.Combine(
                    tempDirectory,
                    $"run_{runNumber++:D6}.txt");

                using (StreamWriter writer = OpenWriter(runPath))
                {
                    while (active.Count > 0)
                    {
                        Record current = HeapPop(active);
                        writer.WriteLine(current.ToString());

                        string? line = reader.ReadLine();

                        if (line == null)
                            continue;

                        Record next = ParseRecord(line);

                        // Для сортування за спаданням: новий ключ має бути <= поточному
                        if (next.Key <= current.Key)
                        {
                            HeapPush(active, next);
                        }
                        else
                        {
                            // This record belongs to the next run.
                            HeapPush(frozen, next);
                        }
                    }
                }

                // Start the next run with the frozen records.
                (active, frozen) = (frozen, active);
            }

            return runsWithFiles(runs, tempDirectory);
        }

        private static List<string> runsWithFiles(
            List<string> runs, string tempDirectory)
        {
            foreach (string file in Directory.GetFiles(
                tempDirectory, "run_*.txt"))
            {
                runs.Add(file);
            }

            runs.Sort(StringComparer.Ordinal);
            return runs;
        }

        private sealed class RunHead
        {
            public Record Record { get; }
            public StreamReader Reader { get; }

            public RunHead(Record record, StreamReader reader)
            {
                Record = record;
                Reader = reader;
            }
        }

        private static void MergeGroupCore(
            List<string> inputRuns, string outputPath)
        {
            var readers = new List<StreamReader>();
            var queue = new PriorityQueue<RunHead, char>(
                new RecordKeyComparer());

            try
            {
                foreach (string path in inputRuns)
                {
                    StreamReader reader = OpenReader(path);
                    readers.Add(reader);

                    string? line = reader.ReadLine();

                    if (line != null)
                    {
                        Record record = ParseRecord(line);
                        queue.Enqueue(
                            new RunHead(record, reader), record.Key);
                    }
                }

                using StreamWriter writer = OpenWriter(outputPath);

                while (queue.Count > 0)
                {
                    RunHead head = queue.Dequeue();
                    writer.WriteLine(head.Record.ToString());

                    string? line = head.Reader.ReadLine();

                    if (line != null)
                    {
                        Record next = ParseRecord(line);
                        queue.Enqueue(
                            new RunHead(next, head.Reader), next.Key);
                    }
                }
            }
            finally
            {
                foreach (StreamReader reader in readers)
                    reader.Dispose();
            }
        }

        // Phase 2: merge runs in bounded groups until one remains.
        private static string MergeAllRuns(
            List<string> runs, string tempDirectory)
        {
            int pass = 0;

            while (runs.Count > 1)
            {
                var nextPass = new List<string>();

                for (int i = 0; i < runs.Count; i += MergeFanIn)
                {
                    var group = runs.GetRange(
                        i, Math.Min(MergeFanIn, runs.Count - i));

                    string outputPath = Path.Combine(
                        tempDirectory,
                        $"merge_{pass}_{nextPass.Count:D6}.txt");

                    MergeGroupCore(group, outputPath);
                    nextPass.Add(outputPath);
                }

                foreach (string oldRun in runs)
                    File.Delete(oldRun);

                runs = nextPass;
                pass++;
            }

            return runs[0];
        }

        public static void Sort(
            string inputPath, string outputPath)
        {
            inputPath = Path.GetFullPath(inputPath);
            outputPath = Path.GetFullPath(outputPath);

            if (string.Equals(
                inputPath, outputPath,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Input and output paths must be different.");
            }

            if (!File.Exists(inputPath))
                throw new FileNotFoundException(
                    "Input file not found.", inputPath);

            string parentDirectory =
                Path.GetDirectoryName(outputPath)!;

            Directory.CreateDirectory(parentDirectory);

            string tempDirectory = Path.Combine(
                parentDirectory,
                "replacement_selection_temp");

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);

            Directory.CreateDirectory(tempDirectory);

            try
            {
                List<string> runs = CreateRuns(
                    inputPath, tempDirectory);

                if (runs.Count == 0)
                {
                    using (OpenWriter(outputPath)) { }
                    return;
                }

                string sortedFile = MergeAllRuns(
                    runs, tempDirectory);

                File.Copy(sortedFile, outputPath, true);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, true);
            }
        }
    }
}