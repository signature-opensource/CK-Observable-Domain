/* tslint:disable */
import { deserialize } from "@signature/json-graph-serializer";

export type WatchEvent = TransactionSetEvent | DomainExportEvent | ErrorEvent | '';

export interface TransactionSetEvent {
    N: number;
    E: any[][];
    L: number;
}

export interface DomainExportEvent {
    N: number;
    C: number;
    P: string[];
    O: any;
    R: number[];
}

export interface ErrorEvent {
    Error: string;
}

export class ObservableDomain {
    private readonly _props: string[];
    private _tranNum: number;
    private _objCount: number;
    private readonly _graph: any[];
    private readonly _roots: any[];

    constructor(initialState?: string | DomainExportEvent | undefined) {
        if (initialState === undefined) {
            this._props = [];
            this._tranNum = 0;
            this._objCount = 0;
            this._graph = [];
            this._roots = [];
        } else {
            const o: DomainExportEvent = typeof (initialState) === "string"
                ? deserialize(initialState, { prefix: "" })
                : initialState;
            this._props = o.P;
            this._tranNum = o.N;
            this._objCount = o.C;
            this._graph = o.O;
            this._roots = o.R.map(i => this._graph[i]);
        }
    }

    public get transactionNumber(): number {
        return this._tranNum;
    }

    public get allObjectsCount(): number {
        return this._objCount;
    }

    public get allObjects(): Iterable<any> {
        function* all(g: any[]) {
            for (const o of g) {
                if (o !== null) yield o;
            }
        }
        return all(this._graph);
    }

    public get roots(): ReadonlyArray<any> {
        return this._roots;
    }

    static isTransactionSetEvent(e: WatchEvent): e is TransactionSetEvent {
        if (e === '') return false;
        return 'E' in e;
    }

    static isDomainExportEvent(e: WatchEvent): e is DomainExportEvent {
        if (e === '') return false;
        return 'P' in e;
    }

    static isErrorEvent(e: WatchEvent): e is ErrorEvent {
        if (e === '') return false;
        return 'Error' in e;
    }

    public applyWatchEvent(e: WatchEvent) {
        if(e === '') {
            this._props.length = 0;
            this._objCount = 0;
            this._graph.length = 0;
            this._roots.length = 0;
            console.warn('Empty OD watch event.');
            return;
        }

        e = deserialize(e, { prefix: "" }); // Resolve objects
        if(ObservableDomain.isTransactionSetEvent(e)) {
            // e.N is the transactionNumber of the LAST event in the array e.E.
            const firstTransactionNumber = e.N - e.E.length + 1;
            if(e.L) {
                // e.L is the LAST transaction that emitted events.
                // If everything is okay, it should be this one.
                if(e.L !== this._tranNum) {
                    throw new Error(`Event claims last event was TN ${e.L}, current TN is ${this._tranNum}`);
                } else {
                    // Fast-forward just before the first event in e.E.
                    this._tranNum = firstTransactionNumber - 1;
                }
            }
            for(let i = 0; i < e.E.length; i++) {
                this.applyEvent(firstTransactionNumber + i, e.E[i]);
            }
        } else if(ObservableDomain.isDomainExportEvent(e)) {
            this.applyDomainExport(e);
        } else if (e === '') {
            throw new Error('The watch event received is empty. The domain doesn\'t exist (or has been destroyed).');
        } else if(ObservableDomain.isErrorEvent(e)) {
            throw new Error('Domain error: ' + e.Error);
        } else {
            throw new Error('Unknown WatchEvent: ' + JSON.stringify(e, undefined, 4))
        }
    }

    public applyDomainExport(e: DomainExportEvent) {
        this._props.splice(0, this._props.length); // Clear array
        for(let i = 0; i < e.P.length; i++) this._props.push(e.P[i]); // Fill array

        this._tranNum = e.N;

        this._objCount = e.C;

        this._graph.splice(0, this._graph.length); // Clear array
        for(let i = 0; i < e.O.length; i++) this._graph.push(e.O[i]); // Fill array

        this._roots.splice(0, this._roots.length); // Clear array
        for(let i = 0; i < e.R.length; i++) this._roots.push(this._graph[e.R[i]]); // Fill array
    }

    public applyEvent(N: number, E: any[]) {
        if (this._tranNum + 1 !== N) {
            throw new Error(`Invalid transaction number. Expected: ${this._tranNum + 1}, got ${N}.`);
        }
        const deleted = new Set<any>();
        const events = E;
        for (let i = 0; i < events.length; ++i) {
            const e = events[i];
            const code: string = e[0];
            switch (code) {
                case "N": // NewObject
                    {
                        let newOne;
                        switch (e[2]) {
                            case "": newOne = {}; break;
                            case "A": newOne = []; break;
                            case "M": newOne = new Map(); break;
                            case "S": newOne = new Set(); break;
                            default: throw new Error(`Unexpected Object type; ${e[2]}. Must be A, M, S or empty string.`);
                        }
                        deleted.delete(e[1]);
                        this._graph[e[1]] = newOne;
                        this._objCount++;
                        break;
                    }
                case "D": // DisposedObject
                    {
                        deleted.add(e[1]);
                        this._objCount--;
                        break;
                    }
                case "P": // NewProperty
                    {
                        if (e[2] != this._props.length) {
                            throw new Error(`Invalid property creation event for '${e[1]}': index must be ${this._props.length}, got ${e[2]}.`);
                        }
                        this._props.push(e[1]);
                        break;
                    }
                case "C":  // PropertyChanged
                    {
                        this._graph[e[1]][this._props[<any>e[2]]] = this.getValue(e[3]);
                        break;
                    }
                case "I":  // ListInsert
                    {
                        const a = this._graph[e[1]];
                        const idx = e[2];
                        const v = this.getValue(e[3]);
                        if (idx === a.length) a[idx] = v;
                        else a.splice(idx, 0, v);
                        break;
                    }
                case "CL": // CollectionClear
                    {
                        const c = this._graph[e[1]];
                        if (c instanceof Array) c.length = 0;
                        else c.clear();
                        break;
                    }
                case "R":  // ListRemoveAt
                    {
                        this._graph[e[1]].splice(e[2], 1);
                        break;
                    }
                case "S":  // ListSetAt
                    {
                        this._graph[e[1]].splice(e[2], 1, this.getValue(e[3]));
                        break;
                    }
                case "K":   // CollectionRemoveKey
                    {
                        this._graph[e[1]].delete(this.getValue(e[2]));
                        break;
                    }
                case "M":   // CollectionMapSet
                    {
                        this._graph[e[1]].set(this.getValue(e[2]), this.getValue(e[3]));
                        break;
                    }
                case "A": // CollectionAddKey
                    {
                        this._graph[e[1]].add(this.getValue(e[2]));
                        break;
                    }
                default: throw new Error(`Unexpected Event code: '${e[0]}'.`);
            }
        }
        deleted.forEach(id => this._graph[id] = null);
        this._tranNum = N;
    }

    private getValue(o: any) {
        if (o != null) {
            var ref = o["="];
            if (ref !== undefined) return this._graph[ref];
        }
        return o;
    }
}



