/* tslint:disable */
import { deserialize } from "@signature/json-graph-serializer";

export interface ObservableDomainEvent {
    N: number;
    E: any[];
}

export interface ObservableDomainState {
    N: number;
    C: number;
    P: string[];
    O: any;
    R: number[];
}

export class ObservableDomain {
    private readonly _props: string[];
    private _tranNum: number;
    private _objCount: number;
    private readonly _graph: any[];
    private readonly  _roots: any[];        

    constructor(initialState: string | ObservableDomainState) {
        const o : ObservableDomainState = typeof (initialState) === "string"
                                            ? deserialize(initialState, { prefix: "" })
                                            : initialState;
        this._props = o.P;
        this._tranNum = o.N;
        this._objCount = o.C;
        this._graph = o.O;
        this._roots = o.R.map(i => this._graph[i]);
    }

    public get transactionNumber() : number { 
        return this._tranNum;
    }

    public get allObjectsCount() : number { 
        return this._objCount;
    }

    public get allObjects() : Iterable<any>  { 
        function* all(g:any[])
        {
            for( const o of g )
            {
                if( o !== null ) yield o;
            }
        }
        return all( this._graph );
     }

    public get roots() : ReadonlyArray<any> { 
        return this._roots;
    }

    public applyEvent(event: ObservableDomainEvent) {
        if (this._tranNum + 1 !== event.N) {
            throw new Error(`Invalid transaction number. Expected: ${this._tranNum + 1}, got ${event.N}.`);
        }

        const events = event.E;
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
                        this._graph[e[1]] = newOne;
                        this._objCount++;
                        break;
                    }
                case "D": // DisposedObject
                    {
                        this._graph[e[1]] = null;
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
                default: throw new Error(`Unexpected Event code: '${e[0]}'.`);
            }
        }
        this._tranNum = event.N;
    }

    private getValue(o: any) {
        if (o != null) {
            var ref = o[">"];
            if (ref !== undefined) return this._graph[ref];
        }
        return o;
    }
}



