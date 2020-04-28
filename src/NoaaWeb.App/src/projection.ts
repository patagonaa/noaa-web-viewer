import * as ko from "knockout";
import 'bootstrap/dist/css/bootstrap.min.css';
import './style.css';

class ProjectionViewModel {
    public darkMode: boolean = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;

    public loading = ko.observable(false);
    public imageLoading = ko.observable(false);

    public currentItem = ko.observable<ProjectionItemViewModel>();
    public pastItems = ko.observableArray<ProjectionItemViewModel>([]);
    public futureItems = ko.observableArray<ProjectionItemViewModel>([]);

    public projectionType = ko.observable<ProjectionTypes>();

    public imgElement = <HTMLImageElement>document.getElementById('projection-image');

    public ignoreHashUpdate = false;

    constructor() {
    }

    async init() {
        await this.refresh(true);
        this.projectionType(this.getProjectionTypes(this.currentItem().projectionTypes)[0].value);

        this.updateImage(); // fire and forget so we can apply bindings while image is still loading

        this.currentItem.subscribe(x => {
            this.ignoreHashUpdate = true;
            window.location.hash = x.fileKey;
            setTimeout(() => this.ignoreHashUpdate = false, 0); // setTimeout here, because the change handler runs _after_, not _during_ this function.
        });
        this.currentItem.subscribe(async () => await this.updateImage());
        this.projectionType.subscribe(() => {
            this.updateImage();
            this.refresh(false);
        });
        window.addEventListener('hashchange', () => {
            if (!this.ignoreHashUpdate)
                this.refresh(true);
        });
    }

    async refresh(updateCurrentItem: boolean) {
        await this.fetchData(window.location.hash.substring(1), updateCurrentItem);
    }

    private loadingAbortController = new AbortController();

    async fetchData(key: string, updateCurrentItem: boolean) {
        if (this.loading()) {
            this.loadingAbortController.abort();
            this.loadingAbortController = new AbortController();
        }
        this.loading(true);
        try {
            let data = await fetch(`api/ProjectionView?fileKey=${key}&projectionType=${this.projectionType() || ''}`, { signal: this.loadingAbortController.signal });
            let result = <ProjectionViewResult>await data.json();
            this.pastItems(result.past);
            this.futureItems(result.future);
            if (updateCurrentItem) {
                this.currentItem(result.current);
            }
            this.loading(false);
        } catch (error) {
            if (error.name !== 'AbortError')
                throw error;
        }
    }

    async updateImage() {
        await this.replaceImage(this.getFilePath(this.currentItem(), this.projectionType()));
        this.preload(this.futureItems()[0]).then(() => this.preload(this.futureItems()[1])).then(() => this.preload(this.pastItems()[0]));
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

        return `data/${item.imageDir}/${item.fileKey}-${enhancementTypeString}-${projectionTypeString}.png`;
    }

    getProjectionTypes(projectionTypes: ProjectionTypes) {
        let toReturn: { key: string, value: ProjectionTypes }[] = [];
        if (projectionTypes & ProjectionTypes.MsaStereographic) {
            toReturn.push({ key: 'MSA Stereographic', value: ProjectionTypes.MsaStereographic });
        }
        if (projectionTypes & ProjectionTypes.MsaMercator) {
            toReturn.push({ key: 'MSA Mercator', value: ProjectionTypes.MsaMercator });
        }
        if (projectionTypes & ProjectionTypes.ThermStereographic) {
            toReturn.push({ key: 'THERM Stereographic', value: ProjectionTypes.ThermStereographic });
        }
        if (projectionTypes & ProjectionTypes.ThermMercator) {
            toReturn.push({ key: 'THERM Mercator', value: ProjectionTypes.ThermMercator });
        }
        return toReturn;
    }

    next() {
        let nextItem = this.futureItems.shift();
        let currentItem = this.currentItem();
        this.currentItem(nextItem);
        this.pastItems.unshift(currentItem);

        this.refresh(false);
    }

    last() {
        let lastItem = this.pastItems.shift();
        let currentItem = this.currentItem();
        this.currentItem(lastItem);
        this.futureItems.unshift(currentItem);

        this.refresh(false);
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