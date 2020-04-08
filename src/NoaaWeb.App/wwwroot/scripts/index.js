

class ListViewModel {
    constructor() {
        this.data = ko.observableArray();
        this.loading = ko.observable(true);

        this.loadPasses();

        window.addEventListener('hashchange', () => this.loadPasses());
    }

    async loadPasses() {
        this.loading(true);

        let appState = this.getAppState();

        let params = new URLSearchParams();

        let sorter = appState.sorter;
        if (sorter) {
            params.set('sortField', sorter.field);
            params.set('sortDir', sorter.dir);
        }

        if (appState.page != null) {
            params.set('page', appState.page);
        }

        let data = await fetch('api/SatellitePass?' + params.toString());
        this.data(await data.json());
        this.loading(false);
    }

    mapPasses(pass) {
        return {
            ...pass,
            enhancementTypes: this.getEnhancementTypes(pass.enhancementTypes),
            startTime: new Date(pass.startTime)
        };
    }

    sortClick(fieldName, direction) {
        let appState = this.getAppState();
        appState.sorter = { field: fieldName, dir: direction };
        appState.page = 0;
        this.setAppState(appState);
    }

    pageClick(page) {
        let appState = this.getAppState();
        appState.page = page;
        this.setAppState(appState);
    }

    getAppState() {
        return window.location.hash ? JSON.parse(decodeURIComponent(window.location.hash.substring(1))) : {};
    }

    setAppState(state) {
        window.location.hash = encodeURIComponent(JSON.stringify(state));
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

    getChannelText(channel) {
        switch (channel) {
            case '1':
                return '1 (visible)';
            case '2':
                return '2 (near infrared)';
            case '3/3B':
                return '3/3B (mid infrared)';
            case '4':
                return '4 (thermal infrared)';
        }
        return channel;
    }
}

function init() {
    ko.applyBindings(new ListViewModel(), document.getElementById("main"));
}

window.onload = init;