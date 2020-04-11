class AppViewModel {
    public data: KnockoutObservableArray<SatellitePassResult>;
    public loading: KnockoutObservable<boolean>;
    public darkMode: boolean = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

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
            params.set('page', appState.page.toString());
        }

        let data = await fetch('api/SatellitePass?' + params.toString());
        this.data(<SatellitePassResult[]>await data.json());
        this.loading(false);
    }

    mapPasses(pass: any) {
        return <SatellitePassViewModel>{
            ...pass,
            enhancementTypes: this.getEnhancementTypes(pass.enhancementTypes),
            startTime: new Date(pass.startTime)
        };
    }

    sortClick(fieldName: string, direction: 'asc'|'desc') {
        let appState = this.getAppState();
        appState.sorter = { field: fieldName, dir: direction };
        appState.page = 0;
        this.setAppState(appState);
    }

    pageClick(page: number) {
        let appState = this.getAppState();
        appState.page = page;
        this.setAppState(appState);
    }

    getAppState(): AppState {
        return window.location.hash ? JSON.parse(decodeURIComponent(window.location.hash.substring(1))) : {};
    }

    setAppState(state: AppState) {
        window.location.hash = encodeURIComponent(JSON.stringify(state));
    }

    getEnhancementTypes(types?: EnhancementTypes) {
        let toReturn: string[] = [];

        if (types == null)
            return toReturn;

        toReturn.push('RAW');

        if (types & EnhancementTypes.Za) {
            toReturn.push('ZA');
        }
        if (types & EnhancementTypes.No) {
            toReturn.push('NO');
        }
        if (types & EnhancementTypes.Msa) {
            toReturn.push('MSA');
        }
        if (types & EnhancementTypes.Mcir) {
            toReturn.push('MCIR');
        }
        if (types & EnhancementTypes.Therm) {
            toReturn.push('THERM');
        }

        return toReturn;
    }

    getEnhancementTypeTitle(type: string) {
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

    getChannelText(channel: string) {
        switch (channel) {
            case '1':
                return '1 (visible)';
            case '2':
                return '2 (near infrared)';
            case '3/3B':
                return '3/3B (mid infrared)';
            case '4':
                return '4 (thermal infrared)';
            case '5':
                return '5 (thermal infrared)';
        }
        return channel;
    }
}

interface AppState {
    page?: number;
    sorter?: { field: string, dir: 'asc' | 'desc' };
    filters: { name: string, value: any }[]
}

interface SatellitePassResult {
    page: number;
    pageCount: number;
    results: any[];
}

interface SatellitePassViewModel {
    fileKey: string;
    startTime: Date;
    satelliteName: string;
    channelA: string;
    channelB: string;
    maxElevation: number;
    gain?: number;
    enhancementTypes?: EnhancementTypes;
    thumbnailUri: string;
    thumbnailEnhancementType: string;
    isUpcomingPass: boolean;
}

enum EnhancementTypes {
    Za = 1 << 0,
    No = 1 << 1,
    Msa = 1 << 2,
    Mcir = 1 << 3,
    Therm = 1 << 4
}

function init() {
    ko.applyBindings(new AppViewModel(), document.getElementById("main"));
}

window.onload = init;