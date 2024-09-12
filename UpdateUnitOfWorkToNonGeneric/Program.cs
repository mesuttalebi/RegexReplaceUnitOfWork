using System.Text.RegularExpressions;

class Program
{
    static bool aPressed = false;
    static void Main()
    {
        Console.WriteLine("Enter the target directory of files:");
        string targetDirectory = Console.ReadLine();

        if (Directory.Exists(targetDirectory))
        {
            var option = string.Empty;
            do
            {
                Console.WriteLine("Select what to do:");
                Console.WriteLine("1. Process all files in the directory to remove TEntity from using(var x = new UnitOfWork<TEntity>()) and add to Repository");
                Console.WriteLine("2. Process all files in the directory to replace any using block regex pattern with a new pattern");
                Console.WriteLine("3. Create Dependency Binding Code for all files");
                Console.WriteLine("0. Exit");

                Console.ForegroundColor = ConsoleColor.Green;
                option = Console.ReadLine();
                Console.ResetColor();

                switch (option)
                {
                    case "1":
                        foreach (var file in Directory.GetFiles(targetDirectory, "*.cs", SearchOption.AllDirectories))
                        {
                            ProcessFile(file);
                        }

                        Console.WriteLine("Processing completed.");
                        break;
                    case "2":
                        ProcessFileForCustomPattern(targetDirectory);

                        break;
                    case "3":
                        ProcessFilesForCreatingDependencyInjectionCode(targetDirectory);
                        break;
                    case "0":
                        break;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
                
            } while (option != "0");
            

            
        }
        else
        {
            Console.WriteLine("The directory does not exist.");
        }
    }

    private static void ProcessFilesForCreatingDependencyInjectionCode(string targetDirectory)
    {
        Console.WriteLine("Enter the DI Module File Path:");
        string diModuleFile = Console.ReadLine().Trim('\"');

        Console.WriteLine("Enter the Line Number to Insert Dependency registration codes...:");
        int lineNumber = int.Parse(Console.ReadLine());
        var diModuleContent = File.ReadAllText(diModuleFile);

        var listOfclassNames = new List<string>();

        foreach (var file in Directory.GetFiles(targetDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string fileContent = File.ReadAllText(file);

            var regex = new Regex(@"public class (\w+)");
            var className = regex.Matches(fileContent).FirstOrDefault();

            if (className != null)
            {
                listOfclassNames.Add(className.Groups[1].Value);
            }
        }

        Console.WriteLine($"Found {listOfclassNames.Count} classes to add to DI registry");

        var lines = diModuleContent.Split(Environment.NewLine).ToList();

        lineNumber -= 1;
        foreach (var className in listOfclassNames)
        {
            lines.Insert(lineNumber++, $"\t\tservices.AddTransient<{className}>();");
        }

        File.WriteAllLines(diModuleFile, lines);
    }

    private static void ProcessFileForCustomPattern(string targetDirectory)
    {
        Console.WriteLine("Enter the regex pattern to search for:");
        Console.WriteLine(@"Example: var\s+(\w+)\s*=\s*new\s*UnitOfWork\s*\(\s*\)");
        Console.WriteLine(@"Not that using block will be automatically added: This will match 'using (var ... = new UnitOfWork()) {' ");

        Console.ForegroundColor = ConsoleColor.Green;
        string pattern = Console.ReadLine();
        Console.ResetColor();

        Console.WriteLine("Enter the new pattern to replace with:");
        Console.WriteLine(@"Example: var $1 = _unitOfWork.Repository<$2>();");

        Console.ForegroundColor = ConsoleColor.Green;
        string newPattern = Console.ReadLine();
        Console.ResetColor();

        foreach (var file in Directory.GetFiles(targetDirectory, "*.cs", SearchOption.AllDirectories))
        {
            ProcessFile(file, pattern, newPattern);
        }
    }

    static void ProcessFile(string filePath)
    {
        string fileContent = File.ReadAllText(filePath);

        // Regex to find the 'using (var ... = new UnitOfWork())' pattern
        Regex usingRegex = new Regex(@"using\s*\(\s*var\s+(\w+)\s*\=\s*new\s*UnitOfWork<(\w+)>\(\s*\)\s*\)\s*\r\n\s*\{", RegexOptions.Compiled);
        var matches = usingRegex.Matches(fileContent);

        // List to hold all changes that need to be made
        var changed = false;

        while (matches.Count > 0)
        {
            var match = matches[0];

            string variableName = match.Groups[1].Value;
            string tEntity = match.Groups[2].Value;
            int startIndex = match.Index;
            int endIndex = FindClosingBracketIndex(fileContent, startIndex + match.Length);

            if (endIndex != -1)
            {
                // Replace all occurrences of the variable name within the block
                string blockContent = fileContent.Substring(startIndex, endIndex - startIndex + 1);

                blockContent = blockContent.Replace($"{variableName}.CommonRepository.", $"{variableName}.CommonRepository<{tEntity}>().");
                
                // Remove the TEntity in using line
                blockContent = blockContent.Replace(match.Value, match.Value.Replace($"<{tEntity}>", ""));

                //var lastClosing = blockContent.LastIndexOf('}');

                //blockContent = blockContent.Substring(0, lastClosing);

                // update file content.
                fileContent = fileContent.Remove(startIndex, endIndex - startIndex + 1);
                fileContent = fileContent.Insert(startIndex, blockContent);

                changed = true;
            }

            matches = usingRegex.Matches(fileContent);
        }

        if (changed)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Processed: {filePath}");
            Console.ResetColor();
            File.WriteAllText(filePath, fileContent);
        }
    }

    static void ProcessFile(string filePath, string pattern, string newPattern)
    {
        string fileContent = File.ReadAllText(filePath);

        // Regex to find the pattern
        //Regex usingRegex = new Regex(@"using\s*\(\s*var\s+(\w+)\s*=\s*new\s*UnitOfWork\s*\(\s*\)\s*\)\s*\{", RegexOptions.Compiled);
        Regex regex = new Regex($@"using\s*\(\s*{pattern}\s*\)\s*\{{", RegexOptions.Compiled);
        var matches = regex.Matches(fileContent);

        Console.WriteLine($"matches found for Pattern {pattern} in file {filePath}: ");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        foreach (var match in matches)
        {
            Console.WriteLine(match);
        }

        Console.ResetColor();
        Console.WriteLine();

        if (!aPressed)
        {
            Console.WriteLine("Press Enter to continue to Next file, Press A to process all files, or Esc to skip.");
            var key = Console.ReadKey();
            if (key.Key == ConsoleKey.Escape)
            {
                return;
            }

            if (key.Key == ConsoleKey.A)
            {
                aPressed = true;
            }
        }

        // List to hold all changes that need to be made
        var changed = false;

        while (matches.Count > 0)
        {
            var match = matches[0];

            int startIndex = match.Index;
            int endIndex = FindClosingBracketIndex(fileContent, startIndex + match.Length);

            if (endIndex != -1)
            {
                // Replace all occurrences of the variable name within the block
                string blockContent = fileContent.Substring(startIndex, endIndex - startIndex + 1);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Block content:\r\n {blockContent}");
                Console.ResetColor();

                // Remove the 'using' statement and the braces
                var firstLine = regex.Replace(match.Value, newPattern);
                blockContent = blockContent.Replace(match.Value, firstLine);

                var lastClosing = blockContent.LastIndexOf('}');

                blockContent = blockContent.Substring(0, lastClosing);

                // update file content.
                fileContent = fileContent.Remove(startIndex, endIndex - startIndex + 1);
                fileContent = fileContent.Insert(startIndex, blockContent);

                changed = true;
            }

            matches = regex.Matches(fileContent);
        }

        if (changed)
        {
            Console.WriteLine($"Processed: {filePath}");
            File.WriteAllText(filePath, fileContent);
        }
    }

    static int FindClosingBracketIndex(string text, int startIndex)
    {
        int openBraces = 0;

        for (int i = startIndex - 1; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                openBraces++;
            }
            else if (text[i] == '}')
            {
                openBraces--;

                if (openBraces == 0)
                {
                    return i;
                }
            }
        }

        // No matching closing brace found
        return -1; 
    }
}
