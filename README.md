

# Introduction

A very simple sound effect library browser. Made for my gamedev work.

* Scan one or more folders for .wav files and extracts tags from filename and metadata.
* Search your library by ore or more pieces of text in tags.
* Play the sound upon selection or `Spacebar`
* Select and export slices of sound to a new .wav file
* Add sounds to 'shortlists' for managing favorites or guiding your searches for sounds

![screenshot](https://github.com/thomasvt/SoundShelf/blob/master/Screenshot.png)

# Shortlists

Shortlists are like favorites, but you can create more than one shortlists. They are primarily used for collecting candidates for a sound you're looking for.

The UI is still quite minimal:

* Create a shortlist in the righthand panel: entering a name and click the + button.
* All shortlists are shown in that same righthand panel.
* Delete a shortlist by clicking the X button next to it.
* Add sounds to shortlists using the `star` button of a sound and toggling shortlist checkboxes.
* See the content of a shortlist by selecting it in the righthand panel. Choose `Show full catalog` to go back to seeing the full library.

# Manage

## Scanning your library

In the Manage tab you can (manually) add rootfolders where your sound files are, then click the either of the Scan buttons.

The `Scan changes` button only processes new and deleted sound files. The `Full rescan` clears your entire library and scans everything again.

## Tag ignore list

SoundShelf collects tags for each sound file, shown in blue boxes in the list. When you want to globally hide certain tags, you can edit the comma separated `tag-ignore` list in the Manage tab. It should apply immediately.

# Where are the config files?

Config files is in your user folder \AppData\Local\SoundShelf. There is a config file holding your settings, and an index file holding the entire sound library and its metadata.
