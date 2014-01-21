#!/bin/bash
# OpenRA packaging script for Debian based distributions

LINUX_BUILD_ROOT="$(readlink -f "$2")"
DEB_BUILD_ROOT=./root

LIBDIR=/usr/lib/openra
DOCDIR=/usr/share/doc/openra

E_BADARGS=85
if [ $# -ne "3" ]
then
    echo "Usage: `basename $0` version root-dir outputdir"
    exit $E_BADARGS
fi
DATE=`echo $1 | grep -o "[0-9]\\+-\\?[0-9]\\?"`
TYPE=`echo $1 | grep -o "^[a-z]*"`
VERSION=$DATE.$TYPE

# Copy template files into a clean build directory (required)
mkdir "$DEB_BUILD_ROOT"
cp -R DEBIAN "$DEB_BUILD_ROOT"
cp -R "$LINUX_BUILD_ROOT"/usr "$DEB_BUILD_ROOT"

# Binaries go in /usr/games
mv "$DEB_BUILD_ROOT"/usr/bin/ "$DEB_BUILD_ROOT"/usr/games/
sed "s|/usr/bin|/usr/games|g" "$DEB_BUILD_ROOT"/usr/games/openra > temp
mv -f temp "$DEB_BUILD_ROOT"/usr/games/openra
chmod +x "$DEB_BUILD_ROOT"/usr/games/openra
sed "s|/usr/bin|/usr/games|g" "$DEB_BUILD_ROOT"/usr/games/openra-editor > temp
mv -f temp "$DEB_BUILD_ROOT"/usr/games/openra-editor
chmod +x "$DEB_BUILD_ROOT"/usr/games/openra-editor

# Put the copyright and changelog in /usr/share/doc/openra/
mkdir -p "$DEB_BUILD_ROOT"/usr/share/doc/openra/
cp copyright "$DEB_BUILD_ROOT"/usr/share/doc/openra/copyright
mv "$DEB_BUILD_ROOT"/usr/lib/openra/AUTHORS "$DEB_BUILD_ROOT"/usr/share/doc/openra
gzip -9 "$DEB_BUILD_ROOT"/usr/share/doc/openra/AUTHORS
DATE=`date -R`

echo -e "openra (${VERSION}) unstable; urgency=low\n" > "$DEB_BUILD_ROOT"/usr/share/doc/openra/changelog
cat "$DEB_BUILD_ROOT"/usr/lib/openra/CHANGELOG >> "$DEB_BUILD_ROOT"/usr/share/doc/openra/changelog
echo -e "\n\n-- Paul Chote <sleipnir@sleipnirstuff.com> ${DATE}" >> root/usr/share/doc/openra/changelog
gzip -9 "$DEB_BUILD_ROOT"/usr/share/doc/openra/changelog
rm "$DEB_BUILD_ROOT"/usr/lib/openra/CHANGELOG
rm "$DEB_BUILD_ROOT"/usr/lib/openra/COPYING

# Create the control file
PACKAGE_SIZE=`du --apparent-size -c "$DEB_BUILD_ROOT"/usr | grep "total" | awk '{print $1}'`
sed "s/{VERSION}/$VERSION/" DEBIAN/control | sed "s/{SIZE}/$PACKAGE_SIZE/" > "$DEB_BUILD_ROOT"/DEBIAN/control

# Build it in the temp directory, but place the finished deb in our starting directory
pushd "$DEB_BUILD_ROOT"

# Calculate md5sums and clean up the ./usr/ part of them
find . -type f -not -path "./DEBIAN/*" -print0 | xargs -0 -n1 md5sum | sed 's|\./usr/|/usr/|' > DEBIAN/md5sums
chmod 0644 DEBIAN/md5sums

# Replace any dashes in the version string with periods
PKGVERSION=`echo $1 | sed "s/-/\\./g"`

# Start building, the file should appear in the output directory
fakeroot dpkg-deb -b . $3/openra_${PKGVERSION}_all.deb

# Clean up
popd
rm -rf "$DEB_BUILD_ROOT"

