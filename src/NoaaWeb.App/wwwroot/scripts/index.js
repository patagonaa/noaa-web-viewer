

class ListViewModel {
    constructor() {
        this.passes = ko.observableArray();
        this.loadPasses();
    }

    async loadPasses() {
        let data = await fetch('api/SatellitePass');
        this.passes((await data.json()).map(x => this.mapPasses(x)));
    }

    mapPasses(pass) {
        return {
            ...pass,
            enhancementTypes: this.getEnhancementTypes(pass.enhancementTypes),
            startTime: new Date(pass.startTime)
        };
    }

    getEnhancementTypes(types) {
        let toReturn = [];
        if (types & (1 << 0)) {
            toReturn.push('ZA');
        }
        if (types & (1 << 1)) {
            toReturn.push('NO');
        }
        if (types & (1 << 2)) {
            toReturn.push('MSA');
        }
        if (types & (1 << 3)) {
            toReturn.push('MCIR');
        }
        if (types & (1 << 4)) {
            toReturn.push('THERM');
        }

        return toReturn;
    }

    getEnhancementTypeTitle(type) {
        switch (type) {
            case 'ZA':
                return 'NOAA general purpose meteorological IR enhancement option.';
            case 'NO':
                return 'NOAA colour IR contrast enhancement option.';
            case 'MSA':
                return 'Multispectral analysis. Determines which regions are most likely to be cloud, land, or sea based on an analysis of the two images.';
            case 'MCIR':
                return 'Colours the NOAA sensor 4 IR image using a map to colour the sea blue and land green. High clouds appear white, lower clouds gray or land / sea coloured.';
            case 'THERM':
                return 'Produces a false colour image from NOAA APT images based on temperature.';
        }
        return '';
    }
}

function init() {
    ko.applyBindings(new ListViewModel(), document.getElementById("main"));
}

window.onload = init;