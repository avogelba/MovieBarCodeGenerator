﻿using Mono.Options;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MovieBarCodeGenerator
{
    public class CLIBatchProcessor
    {
        private BarCodeParametersValidator _barCodeParametersValidator = new BarCodeParametersValidator();
        private FfmpegWrapper _ffmpegWrapper = new FfmpegWrapper("ffmpeg.exe");
        private ImageProcessor _imageProcessor = new ImageProcessor();

        public void Process(string[] args)
        {
            var arguments = new RawArguments();

            var options = new OptionSet();

            options.Add("h|?|help",
                "Show this help message.",
                x => ShowHelp(options));

            options.Add("in|input=",
                "Input file or directory. Required.",
                x => arguments.RawInput = x);

            options.Add("out|output:",
                "Output file or directory. Default: current directory.",
                x => arguments.RawOutput = x);

            options.Add("x|overwrite",
                "If set, existing files will be overwritten instead of being ignored.",
                x => arguments.Overwrite = true);

            options.Add("r|recursive",
                "If set, input is browsed recursively.",
                x => arguments.Recursive = true);

            options.Add("w|width:",
                $"Width of the output image. Default: {RawArguments.DefaultWidth}",
                x => arguments.RawWidth = x);

            options.Add("H|height:",
                "Height of the output image. If this argument is not set, the input height will be used.",
                x => arguments.RawHeight = x);

            options.Add("b|barwidth|barWidth:",
                $"Width of each bar in the output image. Default: {RawArguments.DefaultBarWidth}",
                x => arguments.RawBarWidth = x);

            options.Add("s|smooth",
                "Also generate a smooth version of the output, suffixed with '_smoothed'.",
                x => arguments.Smooth = true);

            options.Parse(args);

            if (arguments.RawHeight == null)
            {
                arguments.UseInputHeight = true;
            }


            string rawInputWithoutWildCards = arguments.RawInput;
            string inputPattern = "*";
            if (arguments.RawInput.Contains('*') || arguments.RawInput.Contains('?'))
            {
                rawInputWithoutWildCards = Path.GetDirectoryName(arguments.RawInput);
                if (rawInputWithoutWildCards == "") // the input is a simple file pattern
                {
                    rawInputWithoutWildCards = ".";
                }
                inputPattern = Path.GetFileName(arguments.RawInput);
            }

            if (Directory.Exists(rawInputWithoutWildCards))
            {
                var searchOption = arguments.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var file in Directory.EnumerateFiles(rawInputWithoutWildCards, inputPattern, searchOption))
                {
                    arguments.RawInput = file; // FIXME: copy instead of changing in place...
                    DealWithOneInputFile(arguments);
                }
            }
            else if (File.Exists(rawInputWithoutWildCards))
            {
                DealWithOneInputFile(arguments);
            }
            else
            {
                Console.WriteLine("Input does not exist.");
            }

            Console.WriteLine($"Exiting...");
        }

        private void DealWithOneInputFile(RawArguments arguments)
        {
            Console.WriteLine($"Processing file '{arguments.RawInput}':");

            CompleteBarCodeGenerationParameters parameters;
            try
            {
                parameters = _barCodeParametersValidator.GetValidatedParameters(
              rawInputPath: arguments.RawInput,
              rawOutputPath: arguments.RawOutput,
              rawBarWidth: arguments.RawBarWidth,
              rawImageWidth: arguments.RawWidth,
              rawImageHeight: arguments.RawHeight,
              useInputHeightForOutput: arguments.UseInputHeight,
              generateSmoothVersion: arguments.Smooth,
              // Choosing whether to overwrite or not is done after validating parameters, not here
              shouldOverwriteOutput: x => true);
            }
            catch (ParameterValidationException ex)
            {
                Console.Error.WriteLine($"Invalid parameters: {ex.Message}");
                return;
            }

            if (File.Exists(parameters.OutputPath) && arguments.Overwrite == false)
            {
                // Check once before generating the image, and once just before saving.
                Console.WriteLine($"WARNING: skipped file {parameters.OutputPath} because it already exists.");
                return;
            }

            var result = _imageProcessor.CreateBarCode(
                parameters.InputPath,
                parameters.BarCode,
                _ffmpegWrapper,
                CancellationToken.None,
                null,
                x => Console.WriteLine(x));

            try
            {
                if (File.Exists(parameters.OutputPath) && arguments.Overwrite == false)
                {
                    // Check once before generating the image, and once just before saving.
                    Console.WriteLine($"WARNING: skipped file {parameters.OutputPath} because it already exists.");
                }
                else
                {
                    result.Save(parameters.OutputPath);
                    Console.WriteLine($"File {parameters.OutputPath} saved successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unable to save the image: {ex}");
            }

            if (parameters.GenerateSmoothedOutput)
            {
                Bitmap smoothed;
                try
                {
                    smoothed = _imageProcessor.GetSmoothedCopy(result);

                    try
                    {
                        if (File.Exists(parameters.OutputPath) && arguments.Overwrite == false)
                        {
                            Console.WriteLine($"WARNING: skipped file {parameters.OutputPath} because it already exists.");
                        }
                        else
                        {
                            smoothed.Save(parameters.SmoothedOutputPath);
                            Console.WriteLine($"File {parameters.SmoothedOutputPath} saved successfully!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Unable to save the smoothed image: {ex}");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"An error occured while creating the smoothed version of the barcode. Error: {ex}");
                }
            }
        }

        private static void ShowHelp(OptionSet options)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            Console.WriteLine($@"Movie BarCode Generator {executingAssembly.GetName().Version}

Generate bar codes from movies. (concatenate movie frames in one image)

You can provide one input file, or a full directory,
along with an output file or directory.
");

            options.WriteOptionDescriptions(Console.Out);
        }
    }

    class RawArguments
    {
        public const string DefaultWidth = "1000";
        public const string DefaultBarWidth = "1";
        public string RawInput { get; set; } = null;
        public string RawOutput { get; set; } = null;
        public bool Overwrite { get; set; } = false;
        public bool Recursive { get; set; } = false;
        public string RawWidth { get; set; } = DefaultWidth;
        public string RawHeight { get; set; } = null;
        public bool UseInputHeight { get; set; } = false;
        public string RawBarWidth { get; set; } = DefaultBarWidth;
        public bool Smooth { get; set; } = false;
    }
}
