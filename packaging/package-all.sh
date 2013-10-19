#!/bin/bash
# OpenRA master packaging script

if [ $# -ne "2" ]; then
	echo "Usage: `basename $0` version outputdir"
    exit 1
fi

# Resolve the absolute source path from the location of this script
SRCDIR=$(readlink -f $(dirname $0)/../)
BUILTDIR="${SRCDIR}/packaging/built"
TAG=$1
OUTPUTDIR=$(readlink -f $2)

# Build the code and push the files into a clean dir
cd "$SRCDIR"
mkdir packaging/built
mkdir packaging/built/mods
make package

# Remove the mdb files that are created during `make`
find . -path "*.mdb" -delete

markdown README.md > README.html
markdown CONTRIBUTING.md > CONTRIBUTING.html
markdown DOCUMENTATION.md > DOCUMENTATION.html

# List of files that are packaged on all platforms
# Note that the Tao dlls are shipped on all platforms except osx and that
# they are now installed to the game directory instead of placed in the gac
FILES=('OpenRA.Game.exe' 'OpenRA.Editor.exe' 'OpenRA.Utility.exe' \
'OpenRA.FileFormats.dll' 'OpenRA.Renderer.SdlCommon.dll' 'OpenRA.Renderer.Cg.dll' 'OpenRA.Renderer.Gl.dll' 'OpenRA.Renderer.Null.dll' 'OpenRA.Irc.dll' \
'FreeSans.ttf' 'FreeSansBold.ttf' \
'cg' 'glsl' 'mods/ra' 'mods/cnc' 'mods/d2k' \
'AUTHORS' 'COPYING' 'HACKING' 'INSTALL' 'CHANGELOG' \
'README.html' 'CONTRIBUTING.html' 'DOCUMENTATION.html' \
'global mix database.dat' 'GeoIP.dll' 'GeoIP.dat')

echo "Copying files..."
for i in "${FILES[@]}"; do
	cp -R "${i}" "packaging/built/${i}" || exit 3
done

# Copy Tao
cp thirdparty/Tao/* packaging/built

# SharpZipLib for zip file support
cp thirdparty/ICSharpCode.SharpZipLib.dll packaging/built

# FuzzyLogicLibrary for improved AI
cp thirdparty/FuzzyLogicLibrary.dll packaging/built

# SharpFont for FreeType support
cp thirdparty/SharpFont* packaging/built

# Mono.NAT for UPnP support
cp thirdparty/Mono.Nat.dll packaging/built

# Copy game icon for windows package
cp OpenRA.Game/OpenRA.ico packaging/built

# Remove demo.mix from cnc
rm ./packaging/built/mods/cnc/bits/demo.mix

#
# Change into packaging directory and run the 
# platform-dependant packaging in parallel
#
cd packaging
echo "Creating packages..."

(
    cd windows
    makensis -DSRCDIR="$BUILTDIR" OpenRA.nsi &> package.log
    if [ $? -eq 0 ]; then
        mv OpenRA.exe "$OUTPUTDIR"/OpenRA-$TAG.exe
    else
        echo "Windows package build failed, refer to windows/package.log."
    fi
) &

(
    cd osx
    sh buildpackage.sh "$TAG" "$BUILTDIR" "$OUTPUTDIR" &> package.log
    if [ $? -ne 0 ]; then
        echo "OS X package build failed, refer to osx/package.log."
    fi
) &

(
    cd linux
    sh buildpackage.sh "$TAG" "$BUILTDIR" "$OUTPUTDIR" &> package.log
    if [ $? -ne 0 ]; then
        echo "Linux package build failed, refer to linux/package.log."
    fi
) &

(
    echo "Downloading source code packages from GitHub."
    curl -s -L -o "$OUTPUTDIR/$TAG.tar.gz" "https://github.com/OpenRA/OpenRA/archive/$TAG.tar.gz"
    if [ $? -ne 0 ]; then
        echo "Source code package download failed."
    fi
) &
wait
echo "Package build done."

rm -rf $BUILTDIR
