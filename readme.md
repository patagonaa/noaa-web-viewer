# noaa-web-viewer

## What do I need to make it work?
right now, the noaa data has to be in a specific directory structure in a WebDAV share (for example: NextCloud)

- / (can be configured)
    - meta/
        - 20200407-195220-NOAA18.txt
    - images/
        - 20200407-195220-NOAA18-RAW.png
        - 20200407-195220-NOAA18-ZA.png
        - 20200407-195220-NOAA18-NO.png
        - 20200407-195220-NOAA18-MSA.png
        - 20200407-195220-NOAA18-MCIR.png
        - 20200407-195220-NOAA18-THERM.png

meta files look something like this:
```
[...]
GAIN=Gain: 13.2
CHAN_A=Channel A: 3/3B (mid infrared)
CHAN_B=Channel B: 4 (thermal infrared)
MAXELEV=80
```

this is designed to work with https://github.com/patagonaa/wx-ground-station (which uploads to WebDAV directly)

## TODO:
- filtering/sorting (server-side)
- actual database (instead of JSON file on disk)
- file watcher for new images (for scraper)
- ???