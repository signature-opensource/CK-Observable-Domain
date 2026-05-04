function isAsciiUpper(ch: string): boolean {
    return ch >= 'A' && ch <= 'Z';
}

function isAsciiLower(ch: string): boolean {
    return ch >= 'a' && ch <= 'z';
}

function isAsciiLetter(ch: string): boolean {
    return isAsciiUpper(ch) || isAsciiLower(ch);
}

export function toCamel(name: string): string {
    if (name.length === 0) return name;
    const c = name[0];
    return isAsciiUpper(c) ? c.toLowerCase() + name.slice(1) : name;
}

export function toPascal(name: string): string {
    if (name.length === 0) return name;
    const c = name[0];
    return isAsciiLower(c) ? c.toUpperCase() + name.slice(1) : name;
}

export function isPlainObject(v: unknown): v is Record<string, unknown> {
    return v !== null
        && typeof v === 'object'
        && Object.getPrototypeOf(v) === Object.prototype;
}

const proxyByTarget: WeakMap<object, object> = new WeakMap();
const isProxy: WeakSet<object> = new WeakSet();

const SHAPE_CACHE: unique symbol = Symbol('DualCasingShapeCache');
const warned: Map<string, Set<string>> = new Map();
let warningsEnabled = true;

function shapeSignatureOf(target: Record<string, unknown>): string {
    const cached = (target as Record<symbol, unknown>)[SHAPE_CACHE];
    if (typeof cached === 'string') return cached;
    const sig = Object.keys(target).sort().join('\x00');
    Object.defineProperty(target, SHAPE_CACHE, {
        value: sig,
        writable: true,
        enumerable: false,
        configurable: true,
    });
    return sig;
}

function clearShapeSignature(target: Record<string, unknown>): void {
    delete (target as Record<symbol, unknown>)[SHAPE_CACHE];
}

function emitWarning(target: Record<string, unknown>, prop: string): void {
    if (!warningsEnabled) return;
    const sig = shapeSignatureOf(target);
    let set = warned.get(sig);
    if (set === undefined) {
        set = new Set<string>();
        warned.set(sig, set);
    }
    if (set.has(prop)) return;
    set.add(prop);
    console.warn(`[ObservableDomain] PascalCase property access '${prop}' is deprecated. Use '${toCamel(prop)}'. Future warnings for this property on this shape are suppressed.`);
}

const handler: ProxyHandler<Record<string, unknown>> = {
    get(target, prop, receiver) {
        if (typeof prop !== 'string') return Reflect.get(target, prop, receiver);
        if (prop.length === 0 || !isAsciiLetter(prop[0])) {
            return Reflect.get(target, prop, receiver);
        }
        const isUpper = isAsciiUpper(prop[0]);
        const alt = isUpper ? toCamel(prop) : toPascal(prop);
        if (alt === prop) return Reflect.get(target, prop, receiver);

        const hasProp = Object.prototype.hasOwnProperty.call(target, prop);
        const hasAlt = Object.prototype.hasOwnProperty.call(target, alt);
        if (hasProp && hasAlt) {
            throw new Error(`[ObservableDomain] Casing clash on read: object has both '${prop}' and '${alt}' as own keys.`);
        }
        if (hasProp) {
            if (isUpper) emitWarning(target, prop);
            return target[prop];
        }
        if (hasAlt) {
            if (isUpper) emitWarning(target, prop);
            return target[alt];
        }
        return undefined;
    },

    set(target, prop, value, receiver) {
        const isStringKey = typeof prop === 'string';
        if (isStringKey && prop.length > 0 && isAsciiLetter(prop[0])) {
            const isUpper = isAsciiUpper(prop[0]);
            const alt = isUpper ? toCamel(prop) : toPascal(prop);
            if (Object.prototype.hasOwnProperty.call(target, alt)) {
                throw new Error(`[ObservableDomain] Casing clash on insert: object already has '${alt}', refusing to add '${prop}'.`);
            }
        }
        if (isStringKey && !Object.prototype.hasOwnProperty.call(target, prop)) {
            clearShapeSignature(target);
        }
        return Reflect.set(target, prop, value, receiver);
    },

    has(target, prop) {
        if (typeof prop !== 'string' || prop.length === 0 || !isAsciiLetter(prop[0])) {
            return Reflect.has(target, prop);
        }
        const isUpper = isAsciiUpper(prop[0]);
        const alt = isUpper ? toCamel(prop) : toPascal(prop);
        if (Reflect.has(target, prop)) return true;
        return alt !== prop && Reflect.has(target, alt);
    },
};

export function wrapDualCasing<T extends object>(target: T): T {
    if (!isPlainObject(target)) return target;
    if (isProxy.has(target)) return target;
    const cached = proxyByTarget.get(target);
    if (cached !== undefined) return cached as T;

    const buckets = new Map<string, string[]>();
    for (const k of Object.keys(target)) {
        if (k.length === 0 || !isAsciiLetter(k[0])) continue;
        const lowerFirst = isAsciiUpper(k[0]) ? toCamel(k) : k;
        let bucket = buckets.get(lowerFirst);
        if (bucket === undefined) {
            bucket = [];
            buckets.set(lowerFirst, bucket);
        }
        bucket.push(k);
    }
    for (const [, names] of buckets) {
        if (names.length >= 2) {
            throw new Error(`[ObservableDomain] Casing clash at wrap time: keys ${names.map(n => `'${n}'`).join(' and ')} resolve to the same camelCase form.`);
        }
    }

    const proxy = new Proxy(target as Record<string, unknown>, handler) as unknown as T;
    proxyByTarget.set(target, proxy as object);
    isProxy.add(proxy as object);
    return proxy;
}

export function wrapDeep<T>(value: T): T {
    return wrapDeepInner(value, new Set<object>());
}

// `seen` guards against the cyclical observable references the OD server
// inlines (e.g. Garage <-> Employees back-pointers). Without this, `wrapDeep`
// recurses indefinitely on the deserialized graph.
function wrapDeepInner<T>(value: T, seen: Set<object>): T {
    if (value === null || typeof value !== 'object') return value;
    if (isProxy.has(value as object)) return value;
    if (seen.has(value as object)) {
        const cached = proxyByTarget.get(value as object);
        return (cached as T) ?? value;
    }
    seen.add(value as object);

    if (Array.isArray(value)) {
        const arr = value as unknown[];
        for (let i = 0; i < arr.length; i++) {
            arr[i] = wrapDeepInner(arr[i], seen);
        }
        return value;
    }
    if (value instanceof Map) {
        for (const [k, v] of value) {
            value.set(k, wrapDeepInner(v, seen));
        }
        return value;
    }
    if (value instanceof Set) {
        const items = Array.from(value);
        value.clear();
        for (const v of items) value.add(wrapDeepInner(v, seen));
        return value;
    }
    if (isPlainObject(value)) {
        // Pre-register the proxy BEFORE recursing into children. Otherwise a
        // cyclic back-pointer hit during recursion finds `seen.has(value)` true
        // but `proxyByTarget.get(value)` undefined (proxy not yet built), and
        // the cycle path falls back to the raw target -- breaking dual-casing
        // reads on the cycle hop. `wrapDualCasing` runs its own bucket-clash
        // check eagerly on the target's own keys (unchanged by child wrapping)
        // so pre-wrapping is safe.
        const proxy = wrapDualCasing(value as Record<string, unknown>) as T;
        for (const k of Object.keys(value)) {
            (value as Record<string, unknown>)[k] = wrapDeepInner((value as Record<string, unknown>)[k], seen);
        }
        return proxy;
    }
    return value;
}

export function setDeprecationWarningsEnabled(enabled: boolean): void {
    warningsEnabled = enabled;
}
