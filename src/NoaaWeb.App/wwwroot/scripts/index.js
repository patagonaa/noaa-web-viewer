

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
        }
    }

    getEnhancementTypes(types) {
        let toReturn = [];
        if (types | (1 << 0)) {
            toReturn.push('ZA');
        }
        if (types | (1 << 1)) {
            toReturn.push('NO');
        }
        if (types | (1 << 2)) {
            toReturn.push('MSA');
        }
        if (types | (1 << 3)) {
            toReturn.push('MCIR');
        }
        if (types | (1 << 4)) {
            toReturn.push('THERM');
        }

        return toReturn;
    }
}

function init() {
    ko.applyBindings(new ListViewModel(), document.getElementById("main"));
}

window.onload = init;