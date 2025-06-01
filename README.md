

# Introduction

A very simple sound effect library browser. Made for my gamedev work.

* Scans one or more folders for .wav files and extracts tags from filename and metadata.
* Search your library by ore or more pieces of text in tags.
* Visualizes and plays wave upon selection or `Spacebar`
* Can export slice of the wave to a new .wav file
* Can add sounds to 'shortlists' for managing favorites or guiding your searches for specific sounds

![screenshot](https://github.com/thomasvt/SoundShelf/blob/master/Screenshot.png)

# Shortlists

Shortlists are like favorites, but you can create multiple shortlists. You create them in the right panel by entering a name and clicking the + button.

You can add sounds to one or more shortlists using the `star` button on each sound.

Select any shortlist in the right panel to see its content. Choose the `Show full catalog` button to go back to the full library.

# Manage

## Scanning your library

In the Manage tab you can (manually) add rootfolders where your sound files are, then click the either of the Scan buttons.

The `Scan changes` button only processes new and deleted sound files. The `Full rescan` clears your entire library and scans everything again.

## Tag ignore list

SoundShelf collects tags for each sound file, shown in blue boxes in the list. When you want to globally hide certain tags, you can edit the comma separated `tag-ignore` list in the Manage tab. It should apply immediately.

# Where are the config files?

Config files is in your user folder \AppData\Local\SoundShelf. There is a config file holding your settings, and an index file holding the entire sound library and its metadata.
