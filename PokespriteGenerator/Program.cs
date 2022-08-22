﻿// See https://aka.ms/new-console-template for more information
using PokespriteGenerator;
using PokespriteGenerator.Models;
using System.CodeDom.Compiler;
using System.Threading.Channels;

// remember System.Formats.Tar is a thing now https://github.com/dotnet/runtime/issues/65951
// https://www.npmjs.com/package/pokesprite-images/v/2.6.0
// https://registry.npmjs.org/pokesprite-images/2.6.0


/*
 * General Idea: https://github.com/pokedextracker/pokesprite
 * 1. Get from NPM
 * 2. Decompress
 * 
 * Then the pipeline/channels work comes in
 * For each pokemon: Pass along the metadata and sprite info to the pipe
 * 3. Create the new css class name based on the metadata
 * 4. modify the image to be the correct size by removing whitespace and scaling
 * 5. pass this to the sitcher which will generate the spritesheet. this is where the pipeline ends
 * 6. pngcrush 
 * 
 * 7. generate the scss 
 */

var npm = new Npm();

var tgzStream = await npm.GetTarball("pokesprite-images");


var decompressor = new Decompressor();

var files = await decompressor.DecompressTgzAsync(tgzStream);

var initialDataChannel = Channel.CreateUnbounded<PokemonData>();
var scaledDataChannel = Channel.CreateUnbounded<PokemonData>();
var trimmedDataChannel = Channel.CreateUnbounded<PokemonData>();

var generator = new PokemonDataGenerator(files, initialDataChannel.Writer);
var scaler = new Scaler(initialDataChannel.Reader, scaledDataChannel.Writer);
var trimmer = new Trimmer(scaledDataChannel.Reader, trimmedDataChannel.Writer);
var spritesheetGenerator = new SpritesheetGenerator(trimmedDataChannel.Reader);

var generatePokemonData = generator.Generate();
var scaledPokemonData = scaler.Scale();
var trimmedPokemonData = trimmer.Trim();
var generateSpritesheet = spritesheetGenerator.GenerateSpritesheet();

await Task.WhenAll(generatePokemonData, scaledPokemonData, trimmedPokemonData, generateSpritesheet);

// we can safely use .Result after a whenall because its done by then
var (pokemonData, spritesheet) = generateSpritesheet.Result;

var scssGenerator = new ScssGenerator();

var scssString = scssGenerator.GenerateScss(pokemonData);

// should probably do some png crushing too

var crudeSaveLocation = new DirectoryInfo(System.Reflection.Assembly.GetExecutingAssembly().Location + @"\..\..\..\..\..\output");
File.WriteAllBytes(Path.Combine(crudeSaveLocation.FullName, "pokesprite.png"), spritesheet);
File.WriteAllText(Path.Combine(crudeSaveLocation.FullName, "pokesprite.css"), scssString);

var testString = pokemonData.Select(x => $$"""<i class="pkicon pkicon-{{x.Number}} {{FormData(x.Form)}}"></i>{{(pokemonData.IndexOf(x) % 32 == 31 ? $"<br />{Environment.NewLine}" : "") }}""");

string FormData(string form)
{
    if (form == "")
    {
        return "";
    }

    return $"form-{form}";
}

var testPage = $$"""
<!DOCTYPE html>
<html>
<head>
	<link rel="stylesheet" href="pokesprite.css">
</head>

<body>
    {{string.Join("", testString)}}    
</body>
</html>
""";

File.WriteAllText(Path.Combine(crudeSaveLocation.FullName, "test.html"), testPage);

Console.WriteLine("Hello, World!");