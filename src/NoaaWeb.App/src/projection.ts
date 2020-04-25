import * as ko from "knockout";
import 'bootstrap/dist/css/bootstrap.min.css';
import './style.css';




class ProjectionViewModel {
    public darkMode: boolean = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

    public loading = ko.observable(true);
    public imageLoading = ko.observable(true);

    public currentItem = ko.observable<ProjectionItemViewModel>();
    public pastItems = ko.observableArray<ProjectionItemViewModel>([]);
    public futureItems = ko.observableArray<ProjectionItemViewModel>([]);

    public projectionType = ko.observable<ProjectionTypes>(ProjectionTypes.MsaStereographic);

    public imgElement = <HTMLImageElement>document.getElementById('projection-image');

    constructor() {
        this.currentItem.subscribe(x => window.location.hash = x.fileKey);
        this.projectionType.subscribe(() => this.refresh());
        window.addEventListener('hashchange', () => this.refresh());
    }

    async init() {
        await this.refresh();
    }

    async refresh() {
        await this.fetchData(window.location.hash.substring(1));
    }

    async fetchData(key: string) {
        this.loading(true);
        let data = await fetch('api/ProjectionView?fileKey=' + key + '&projectionType=' + this.projectionType());
        let result = <ProjectionViewResult>await data.json();
        this.pastItems(result.past);
        this.futureItems(result.future);
        this.currentItem(result.current);
        this.loading(false);

        await this.replaceImage(this.getFilePath(this.currentItem(), this.projectionType()));

        this.preload(result.future[0]).then(() => this.preload(result.future[1])).then(() => this.preload(result.past[0]));
    }

    async replaceImage(src: string) {
        this.imageLoading(true);
        await new Promise((resolve, reject) => {
            this.imgElement.src = src;
            this.imgElement.onload = resolve;
            this.imgElement.onerror = reject;
        });
        this.imageLoading(false);
    }

    async preload(item: ProjectionItemViewModel) {
        if (item == null)
            return;
        await new Promise((resolve, reject) => {
            let img = new Image();
            img.src = this.getFilePath(item, this.projectionType());
            img.onload = resolve;
            img.onerror = reject;
        });
    }

    getFilePath(item: ProjectionItemViewModel, projectionType: ProjectionTypes) {
        let enhancementTypeString = '';
        let projectionTypeString = '';

        switch (projectionType) {
            case ProjectionTypes.MsaMercator:
                enhancementTypeString = 'MSA';
                projectionTypeString = 'merc';
                break;
            case ProjectionTypes.MsaStereographic:
                enhancementTypeString = 'MSA';
                projectionTypeString = 'stereo';
                break;
            case ProjectionTypes.ThermMercator:
                enhancementTypeString = 'THERM';
                projectionTypeString = 'merc';
                break;
            case ProjectionTypes.ThermStereographic:
                enhancementTypeString = 'THERM';
                projectionTypeString = 'stereo';
                break;
        }

        return 'data/' + item.imageDir + '/' + item.fileKey + '-' + enhancementTypeString + '-' + projectionTypeString + '.png';
    }

    getProjectionTypes(projectionTypes: ProjectionTypes) {
        let toReturn: { key: string, value: ProjectionTypes }[] = [];
        if (projectionTypes & ProjectionTypes.MsaMercator) {
            toReturn.push({ key: 'MSA Mercator', value: ProjectionTypes.MsaMercator });
        }
        if (projectionTypes & ProjectionTypes.MsaStereographic) {
            toReturn.push({ key: 'MSA Stereographic', value: ProjectionTypes.MsaStereographic });
        }
        if (projectionTypes & ProjectionTypes.ThermMercator) {
            toReturn.push({ key: 'THERM Mercator', value: ProjectionTypes.ThermMercator });
        }
        if (projectionTypes & ProjectionTypes.ThermStereographic) {
            toReturn.push({ key: 'THERM Stereographic', value: ProjectionTypes.ThermStereographic });
        }
        return toReturn;
    }

    next() {
        let nextItem = this.futureItems.shift();
        let currentItem = this.currentItem();
        this.currentItem(nextItem);
        this.pastItems.unshift(currentItem);

        this.refresh();
    }

    last() {
        let lastItem = this.pastItems.shift();
        let currentItem = this.currentItem();
        this.currentItem(lastItem);
        this.futureItems.unshift(currentItem);

        this.refresh();
    }
}

interface ProjectionViewResult {
    past: ProjectionItemViewModel[];
    current: ProjectionItemViewModel;
    future: ProjectionItemViewModel[];
}

interface ProjectionItemViewModel {
    fileKey: string;
    imageDir: string;
    projectionTypes: ProjectionTypes;
}

enum ProjectionTypes {
    None = 0,
    MsaStereographic = 1 << 1,
    MsaMercator = 1 << 2,
    ThermStereographic = 1 << 3,
    ThermMercator = 1 << 4
}

async function init() {
    let model = new ProjectionViewModel();
    await model.init();
    ko.applyBindings(model, document.getElementById("main"));
}

window.onload = init;