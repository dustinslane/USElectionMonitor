# USElectionMonitor
Monitor that pulls AP vote count feed every minute and report diffs

![](https://x.dustinslane.nl/img/FarflungArizonaalligatorlizardAccomplishWren-3542dab0.png)

## Download

#### [Download here](https://github.com/dustinslane/USElectionMonitor/releases/latest)


## How to use

Download the latest release zip, run the .exe. It will create a `Config.json` file for you. You can use it to tweak some settings and filter results. See below.

## Config

Config example:
```json
{
  "ImportantStates": [
    "PA",
    "GA",
    "AZ",
    "NV",
    "NC"
  ],
  "ResultsEndpoint": "https://interactives.ap.org/elections/live-data/production/2020-11-03/president/summary.json",
  "ManifestEndpoint": "https://interactives.ap.org/elections/live-data/production/2020-11-03/president/metadata.json",
  "FilterPresidentLastNames": [
    "Biden",
    "Trump"
  ]
}
``` 

`ImportantStates`: add states here to colour yellow  
`ResultsEndpoint`: endpoint for AP results data  
`ManifestEndpoint`: endpoint for AP manifest data  
`FilterPresidentLastNames`: Add last names of candidates here. If no names are listed, every result that comes in is shown. Add names to only show results for names listed.  


### Disclaimer:

This program pulls data from the AP endpoint. I am not responsible for anything. 

### Buy me a beer

https://streamlabs.com/dustin_slane/tip
