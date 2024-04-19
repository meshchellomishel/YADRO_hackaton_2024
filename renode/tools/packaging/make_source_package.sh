#!/usr/bin/env bash

set -e
set -u

# Change dir to script location
cd "${0%/*}"
. common_make_packages.sh

DIR=renode_${VERSION}_source
OUTPUT=output/packages 
OUTPUT_DIR=$BASE/$OUTPUT 
PACKAGE_NAME=$DIR.tar.xz
TMP=$(mktemp -d)

# Create a copy of the local Renode directory
cp -r $BASE $TMP
mv $TMP $DIR
rm -rf $TMP

# Remove the output folder
rm -rf $DIR/output

# Remove all build and git files
find $DIR -iname bin -type d -o -iname obj -type d -o \
-iname .git -o -iname .gitignore -o -iname .gitmodules -o -iname .gitattributes | xargs rm -rf

mkdir -p $OUTPUT_DIR
# Compress with xz to the output folder using 4 threads
tar --use-compress-program='xz -T4' -cf $OUTPUT_DIR/$PACKAGE_NAME $DIR

echo "Created a source code package in $OUTPUT/$PACKAGE_NAME"

if $REMOVE_WORKDIR
then
    rm -rf $DIR
fi
