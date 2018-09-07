import { deserialize } from '@signature/json-graph-serializer';

export function ObservableDomain(initialState /*: string|object*/) {
    var o = typeof (initialState) === "string"
        ? deserialize(initialState, { prefix: "" })
        : initialState;
    var props = o.P;
    this.tranNum = o.N;
    this.graph = liftGraphContainerContent(o.O);

    function liftGraphContainerContent(g) {
        const len = g.length;
        for (let i = 0; i < len; ++i) {
            const o = g[i];
            if (o && typeof (o) === "object") {
                for (let p in o) {
                    const v = o[p];
                    if (v && typeof (v) === "object") {
                        const c = v["$C"];
                        if (c !== undefined) {
                            o[p] = g[v["$i"]];
                        }
                    }
                }
            }
        }
        return g;
    }

    this.roots = o.R.map(i => this.graph[i]);

    this.applyEvent = function (event) {
        const getValue = o => {
            if (o != null) {
                var ref = o[">"];
                if (ref !== undefined) return this.graph[ref];
            }
            return o;
        }

        if (this.tranNum + 1 > event.N) throw new Error(`Invalid transaction number. Expected: greater than ${this.tranNum + 1}, got ${event.N}.`);
        const events = event.E;
        for (let i = 0; i < events.length; ++i) {
            const e = events[i];
            const code = e[0];
            switch (code) {
                case "N":  // NewObject
                    {
                        let newOne;
                        switch (e[2]) {
                            case "": newOne = {}; break;
                            case "A": newOne = []; break;
                            case "M": newOne = new Map(); break;
                            case "S": newOne = new Set(); break;
                            default: throw new Error(`Unexpected Object type; ${e[2]}. Must be A, M, S or empty string.`);
                        }
                        this.graph[e[1]] = newOne;
                        break;
                    }
                case "D":  // DisposedObject
                    {
                        this.graph[e[1]] = null;
                        break;
                    }
                case "P":  // NewProperty
                    {
                        if (e[2] != props.length) throw new Error(`Invalid property creation event for '${e[1]}': index must be ${props.length}, got ${e[2]}.`);
                        props.push(e[1]);
                        break;
                    }
                case "C":  // PropertyChanged
                    {
                        this.graph[e[1]][props[e[2]]] = getValue(e[3]);
                        break;
                    }
                case "I":  // ListInsert
                    {
                        const a = this.graph[e[1]];
                        const idx = e[2];
                        const v = getValue(e[3]);
                        if (idx === a.length) a[idx] = v;
                        else a.splice(idx, 0, v);
                        break;
                    }
                case "CL": // CollectionClear
                    {
                        const c = this.graph[e[1]];
                        if (c instanceof Array) c.length = 0;
                        else c.clear();
                        break;
                    }
                case "R":  // ListRemoveAt
                    {
                        this.graph[e[1]].splice(e[2], 1);
                        break;
                    }
                case "S":  // ListSetAt
                    {
                        this.graph[e[1]].splice(e[2], 1, getValue(e[3]));
                        break;
                    }
                case "K":   // CollectionRemoveKey
                    {
                        this.graph[e[1]].delete(getValue(e[2]));
                        break;
                    }
                default: throw new Error(`Unexpected Event code: '${e[0]}'.`);
            }
        }
        this.tranNum = event.N;

    }

}