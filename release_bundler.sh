#!/bin/bash

set -e

dotnet build -c Release

rm -rf Multiplayer-Compatibility/
mkdir -p Multiplayer-Compatibility

cp -r About Assemblies Referenced Languages Multiplayer-Compatibility

# Zip for Github releases
rm -f Multiplayer-Compatibility.zip
zip -r -q Multiplayer-Compatibility.zip Multiplayer-Compatibility

echo "Ok, $PWD/Multiplayer-Compatibility.zip ready"
