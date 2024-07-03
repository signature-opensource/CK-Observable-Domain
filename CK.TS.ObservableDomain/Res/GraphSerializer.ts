/**
 * Defines options of the {@link serialize}.
 */
export interface ISerializeOptions {
    /**
     *  Defaults to "~$£€".
     */
    prefix?: string;

    /**
     * Optional function that can substitute an object before serialization.
     * Can return the "v" object or a falsy value when no substitution must be made. 
     * The "rT" parameter is .
     * @param v The object about to be serialized.
     * @param rT The type indicator (depends on the prefix).
     * @returns A substituted object or the "v" object or a falsy value when no substitution must be made.
     */
    substitor?: (v: any, rT: string) => any;
}

/**
 * Defines options of the {@link deserialize}.
 */
export interface IDeserializeOptions {
    /**
     *  Defaults to "~$£€".
     */
    prefix?: string;

    /**
     * Optional function that can change the deserialized instance.
     * @param v The deserilized instance.
     * @param rT The type indicator (depends on the prefix).
     * @returns The final object.
     */
    activator?: (v: any, rT: string) => any;
}


/**
 * Serializes an object in JSON that can contain internal relationships.
 * Serialized objects contain a "<prefix>i" field that is the index
 * in the breadth-first traversal of the graph done by JSON.stringify().
 * References to already serialized objects are exposed as single-property
 * objects like { "<prefix>r": idx }.
 * The deserialize function uses the index to restore the complete graph.
 * Parameter prefix is optional. It defaults to "~$£€".
 * @param {object} o - The object to serialize
 * @param {object} options - Serialization options: prefix (default '~$£€'), substitor
 * @return {string} The serialized value.
 */
export function serialize(o: object, options?: ISerializeOptions): string {
    const { prefix, substitor } = Object.assign({ prefix: "~$£€" }, options);
    const rR = prefix + ">";
    const rI = prefix + "°";
    const rT = prefix + "þ";
    const marker = Symbol();
    const cleanup = new Array<any>();
    try {
        let c = 0;
        function markObj(o: any) { o[marker] = 1; return o; }
        function markNum(n: number) { return markObj(new Number(n)); }
        function markTyp(s: string) { return markObj(new String(s)); }
        return JSON.stringify(o, function (k, v) {
            if (k === rR || k === rI || k === rT) {
                if (v !== null && v[marker]) return v;
                throw new Error("Conflicting serialization prefix: property '" + k + "' exists.");
            }
            if (v === null || typeof v !== "object" || v[marker]) return v;
            let ref = v[rI];
            if (ref) {
                if (ref[marker]) return { [rR]: ref };
                throw new Error("Conflicting serialization prefix: property '" + rI + "' exists.");
            }
            v[rI] = ref = markNum(c++);
            cleanup.push(v);
            if (v instanceof Array) {
                v = markObj([markObj({ [rT]: markObj([ref, "A"]) }), ...v]);
            }
            else if (v instanceof Map) {
                v = markObj([markObj({ [rT]: markObj([ref, "M"]) }), ...[...v].map(e => markObj(e))]);
            }
            else if (v instanceof Set) {
                v = markObj([markObj({ [rT]: markObj([ref, "S"]) }), ...v]);
            }
            else if (substitor) {
                let sv = substitor(v, rT);
                if (sv && sv !== v) {
                    v = sv;
                    v[rI] = ref;
                    v[rT] = markTyp(v[rT] || "");
                }
            }
            return v;
        });
    }
    finally {
        cleanup.forEach(o => delete o[rI]);
    }
};

/**
 * Deserializes a previously-serialized object graph.
 * Parameter prefix is optional and defaults to "~$£€": it must, of course,
 * be the same as the prefix used to serialize the graph.
 * @param {(string|object)} o - The serialized string, or parsed object.
 * @param {object} options - Deserialization options.
 * @return {object} The deserialized value.
 */
export function deserialize(s: string | object, options?: IDeserializeOptions): any {
    const { prefix, activator } = Object.assign({ prefix: "~$£€" }, options);
    const rR = prefix + ">";
    const rI = prefix + "°";
    const rT = prefix + "þ";
    // Collects externally activated instances so that they won't be
    // processed.
    let extRef: Set<any> | null = null;
    // Collects untracked objects  so that they will be processed.
    let plainObjs: Set<any> | null = null;
    const map = new Array<any>();

    function rev(k: any, v: any) {
        let type = null;
        if (v instanceof Array) {
            if (v.length > 0
                && v[0] != null
                && (type = v[0][rT]) !== undefined) {
                v.splice(0, 1);
                switch (type[1]) {
                    case "A": break;
                    case "M": v = Object.assign(new Map(), { "v": v }); break;
                    case "S": v = Object.assign(new Set(), { "v": v }); break;
                    default: throw new Error("Expecting typed array to be 'A', 'M' or 'S'.");
                }
                map[type[0]] = v;
            }
            else {
                if (plainObjs === null) plainObjs = new Set();
                plainObjs.add(v);
            }
        }
        else if (v !== null) {
            const idx = v[rI];
            if (idx !== undefined) {
                delete v[rI];
                if ((type = v[rT]) !== undefined) {
                    delete v[rT];
                    if (activator) {
                        v = activator(v, type);
                        if (v) {
                            if (extRef === null) extRef = new Set<any>();
                            extRef.add(v);
                        }
                    }
                }
                map[idx] = v;
            }
            else {
                if (plainObjs === null) plainObjs = new Set();
                plainObjs.add(v);
            }
        }
        return v;
    }

    // This simple depth-first traversal applies the reviver to an already 
    // JSON parsed tree.
    function d(o: any) {
        if (o) {
            if (o instanceof Array) {
                for (let i = 0; i < o.length; ++i) {
                    const v = o[i];
                    d(v);
                    o[i] = rev(i, v);
                }
            }
            else if (typeof (o) === "object") {
                for (const p in o) {
                    const v = o[p];
                    d(v);
                    o[p] = rev(p, v);
                }
            }
        }
        return o;
    }
    // If its a string, JSON.parse and the reviver handle the first step: registering the 
    // objects in map array and any external references into extRef set.   
    const o = typeof (s) === "string"
        ? JSON.parse(s, rev)
        : rev(undefined, d(s));

    // Second step is to handle the collections (array, map and set).
    function processA(map: Array<any>, a: Array<any>) {
        const len = a.length;
        for (let i = 0; i < len; ++i) {
            const c = a[i];
            if (c) {
                const ref = c[rR];
                if (ref !== undefined) a[i] = map[ref];
            }
        }
    }
    for (let i of map) {
        if (extRef === null || !(<Set<any>>extRef).has(i)) {
            if (i instanceof Array) {
                processA(map, i);
            }
            else if (i instanceof Map) {
                (<Map<any, any>>(<any>i).v).forEach(e => processA(map, e));
                (<Map<any, any>>(<any>i).v).forEach(e => i.set(e[0], e[1]));
                delete (<any>i).v;
            }
            else if (i instanceof Set) {
                processA(map, (<any>i).v);
                (<Set<any>>(<any>i).v).forEach(e => i.add(e));
                delete (<any>i).v;
            }
            else {
                for (const p in i) {
                    const o = i[p];
                    if (o !== null) {
                        const ref = o[rR];
                        if (ref !== undefined) i[p] = map[ref];
                    }
                }
            }
        }
    }
    if (plainObjs !== null) {
        for (let i of <Set<any>>plainObjs) {
            if (i instanceof Array) {
                processA(map, i);
            }
            else {
                for (const p in i) {
                    const o = i[p];
                    if (o !== null) {
                        const ref = o[rR];
                        if (ref !== undefined) i[p] = map[ref];
                    }
                }
            }
        }
    }
    return o;
}
