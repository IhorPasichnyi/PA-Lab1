using System;
using System.Diagnostics;

namespace ExternalSortingAlgorithms
{
    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.Write("Введіть назву файлу (без розширення): ");
            string? fileName = Console.ReadLine();

            long sizeInMb;
            bool isValid = false;
            do
            {
                Console.Write("Введіть розмір файла (МБ): ");
                if (long.TryParse(Console.ReadLine(), out sizeInMb) && sizeInMb > 0)
                {
                    isValid = true;
                }
                else
                {
                    Console.WriteLine("Помилка: некоректний розмір файла!");
                }
            } while (!isValid);
            Console.WriteLine();

            string path = $"D:\\University\\3 Semester\\PA\\Lab 1\\Program\\files\\{fileName}.txt";
            Console.WriteLine("Генерація файла...");
            FileGenerator.GenerateFile(path, sizeInMb);
            Console.WriteLine($"Згенеровано файл {fileName} розміром {sizeInMb} MB.");
            Console.WriteLine();

            string inputFilePath = $"D:\\University\\3 Semester\\PA\\Lab 1\\Program\\files\\{fileName}.txt";
            string sortedFilePath = $"D:\\University\\3 Semester\\PA\\Lab 1\\Program\\files\\sorted files\\{fileName}.txt";

            // Пряме злиття
            Console.WriteLine();
            Console.WriteLine("Початок сортування базовим алгоритмом прямого злиття...");
            Stopwatch stopwatch1 = Stopwatch.StartNew();
            StraightMergeSorter.Sort(inputFilePath, sortedFilePath);
            stopwatch1.Stop();
            Console.WriteLine($"Сортування завершено за: {stopwatch1.Elapsed.TotalSeconds:F2} c.");
            
            // Модифіковане злиття
            Console.WriteLine();
            Console.WriteLine("Початок сортування модифікованим алгоритмом прямого злиття...");
            Stopwatch stopwatch2 = Stopwatch.StartNew();
            ModifiedMergeSorter.Sort(inputFilePath, sortedFilePath);
            stopwatch2.Stop();
            Console.WriteLine($"Сортування завершено за: {stopwatch2.Elapsed.TotalSeconds:F2} c.");

            // Версія ChatGPT
            Console.WriteLine();
            Console.WriteLine("Початок сортування алгоритмом Replacement Selection Sort (згенерований ChatGPT)...");
            Stopwatch stopwatch3 = Stopwatch.StartNew();
            ReplacementSelectionSorter.Sort(inputFilePath, sortedFilePath);
            stopwatch3.Stop();
            Console.WriteLine($"Сортування завершено за: {stopwatch3.Elapsed.TotalSeconds:F2} c.");

            // Версія Gemini
            Console.WriteLine();
            Console.WriteLine("Початок сортування алгоритмом External Radix Sort (згенерований Gemini)...");
            Stopwatch stopwatch4 = Stopwatch.StartNew();
            ExternalRadixSorter.Sort(inputFilePath, sortedFilePath);
            stopwatch4.Stop();
            Console.WriteLine($"Сортування завершено за: {stopwatch4.Elapsed.TotalSeconds:F2} c.");
            
            // Версія Claude
            Console.WriteLine();
            Console.WriteLine("Початок сортування алгоритмом External Quick Sort (згенерований Claude)...");
            Stopwatch stopwatch5 = Stopwatch.StartNew();
            ExternalQuickSorter.Sort(inputFilePath, sortedFilePath);
            stopwatch5.Stop();
            Console.WriteLine($"Сортування завершено за: {stopwatch5.Elapsed.TotalSeconds:F2} c.");
        }
    }
}