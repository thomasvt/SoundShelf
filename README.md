

# Introduction

A very simple sound effect library browser. Made for my gamedev work.

* Scans one or more folders for .wav files
* Can search your library by name and tags deduced from filename and wav metadata.
* Plays sound upon selection or `Spacebar`
* Visualizes wave, lets you select a slice with the mouse
* Extracts slice to new .wav file

![screenshot](https://github.com/thomasvt/SoundShelf/blob/master/Screenshot.png)

# Manage

## Scanning your library

In the Manage tab you can (manually) add rootfolders where your sound files are, then click the either of the Scan buttons.

The `Scan changes` button only processes new and deleted sound files. The `Full rescan` clears your entire library and scans everything again.

## Tag ignore list

SoundShelf collects tags for each sound file, shown in blue boxes in the list. When you want to globally hide certain tags, you can edit the comma separated `tag-ignore` list in the Manage tab. It should apply immediately.

# Where are the config files?

Config files is in your user folder \AppData\Local\SoundShelf. There is a config file holding your settings, and an index file holding the entire sound library and its metadata.
