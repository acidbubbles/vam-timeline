#!/bin/bash

set -e
set -o pipefail

rm -rf output
mkdir output

for cslistFile in *.cslist
do
  echo "Processing $cslistFile"
  temp_header=$(mktemp)
  trap "rm -f $temp_header" 0 2 3 15
  temp_body=$(mktemp)
  trap "rm -f $temp_body" 0 2 3 15
  tac $cslistFile | dos2unix | while read csFile
  do
    echo "  Adding: $csFile"
    cat $csFile | dos2unix | sed -n '/^using.*/p' >> $temp_header
    cat $csFile | dos2unix | sed -n '/^namespace/,$p' >> $temp_body
  done
  outputFile=output/${cslistFile::-4}
  echo "  Writing $outputFile"
  echo "// VamTimeline, by Acid Bubbles" > $outputFile
  echo "// https://github.com/acidbubbles/vam-timeline" >> $outputFile
  cat $temp_header | sort -u >> $outputFile
  echo "" >> $outputFile
  cat $temp_body | sed '\,^ *//,d' >> $outputFile

  unix2dos $outputFile
done
