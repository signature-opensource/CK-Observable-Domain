import {
    wrapDualCasing,
    wrapDeep,
    setDeprecationWarningsEnabled,
    isPlainObject,
    toCamel,
    toPascal,
} from '@local/ck-gen';

describe('DualCasingProxy — helpers', () => {
    describe('toCamel', () => {
        it('lowercases the first ASCII upper', () => {
            expect(toCamel('Code')).toBe('code');
            expect(toCamel('A')).toBe('a');
            expect(toCamel('URL')).toBe('uRL');
        });
        it('returns input unchanged when first char is not ASCII upper', () => {
            expect(toCamel('code')).toBe('code');
            expect(toCamel('_internal')).toBe('_internal');
            expect(toCamel('1foo')).toBe('1foo');
            expect(toCamel('')).toBe('');
        });
    });

    describe('toPascal', () => {
        it('uppercases the first ASCII lower', () => {
            expect(toPascal('code')).toBe('Code');
            expect(toPascal('a')).toBe('A');
            expect(toPascal('uRL')).toBe('URL');
        });
        it('returns input unchanged when first char is not ASCII lower', () => {
            expect(toPascal('Code')).toBe('Code');
            expect(toPascal('_internal')).toBe('_internal');
            expect(toPascal('1foo')).toBe('1foo');
            expect(toPascal('')).toBe('');
        });
    });

    describe('isPlainObject', () => {
        it('is true for `{}` and object literals', () => {
            expect(isPlainObject({})).toBe(true);
            expect(isPlainObject({ a: 1 })).toBe(true);
        });
        it('is false for arrays, Map, Set, null, primitives, classes', () => {
            expect(isPlainObject([])).toBe(false);
            expect(isPlainObject(new Map())).toBe(false);
            expect(isPlainObject(new Set())).toBe(false);
            expect(isPlainObject(null)).toBe(false);
            expect(isPlainObject(undefined)).toBe(false);
            expect(isPlainObject(42)).toBe(false);
            expect(isPlainObject('hello')).toBe(false);
            class C {}
            expect(isPlainObject(new C())).toBe(false);
        });
    });
});

describe('wrapDualCasing', () => {
    it('returns input unchanged for non-plain values', () => {
        const arr: unknown[] = [];
        const map = new Map<string, unknown>();
        const set = new Set<unknown>();
        class C { x = 1; }
        const c = new C();
        expect(wrapDualCasing(arr)).toBe(arr);
        expect(wrapDualCasing(map)).toBe(map);
        expect(wrapDualCasing(set)).toBe(set);
        expect(wrapDualCasing(c)).toBe(c);
    });

    it('returns a Proxy distinct from the target for plain objects', () => {
        const target = { Code: 'X' };
        const proxy = wrapDualCasing(target);
        expect(proxy).not.toBe(target);
    });

    it('is idempotent: re-wrapping the same target returns the same proxy', () => {
        const target = { Code: 'X' };
        const p1 = wrapDualCasing(target);
        const p2 = wrapDualCasing(target);
        expect(p2).toBe(p1);
    });

    it('is idempotent: wrapping an existing proxy returns the proxy itself', () => {
        const target = { Code: 'X' };
        const p1 = wrapDualCasing(target);
        const p2 = wrapDualCasing(p1);
        expect(p2).toBe(p1);
    });

    it('throws at wrap time when the same target has both casings of a property', () => {
        expect(() => wrapDualCasing({ Code: 'X', code: 'Y' })).toThrow(/Casing clash at wrap time/);
    });

    it('does not throw when only one casing is present', () => {
        expect(() => wrapDualCasing({ Code: 'X', otherField: 1 })).not.toThrow();
        expect(() => wrapDualCasing({ code: 'X', OtherField: 1 })).not.toThrow();
    });
});

describe('Proxy get trap', () => {
    it('returns value when accessed with the storage casing', () => {
        const p = wrapDualCasing({ Code: 'X' });
        expect((p as Record<string, unknown>)['Code']).toBe('X');
    });

    it('returns value when accessed with the alternate casing (camel reads pascal storage)', () => {
        const p = wrapDualCasing({ Code: 'X' });
        expect((p as Record<string, unknown>)['code']).toBe('X');
    });

    it('returns value when accessed with the alternate casing (pascal reads camel storage)', () => {
        const p = wrapDualCasing({ code: 'X' });
        expect((p as Record<string, unknown>)['Code']).toBe('X');
    });

    it('returns undefined for a property neither casing has', () => {
        const p = wrapDualCasing({ Code: 'X' });
        expect((p as Record<string, unknown>)['missing']).toBeUndefined();
        expect((p as Record<string, unknown>)['Missing']).toBeUndefined();
    });

    it('passes through Symbol-keyed access without casing logic', () => {
        const sym = Symbol('marker');
        const target = { Code: 'X', [sym]: 'tagged' } as Record<PropertyKey, unknown>;
        const p = wrapDualCasing(target);
        expect((p as Record<PropertyKey, unknown>)[sym]).toBe('tagged');
    });

    it('passes through non-letter-prefixed property access unchanged', () => {
        const p = wrapDualCasing({ _internal: 1, $ref: 'r' } as Record<string, unknown>);
        expect((p as Record<string, unknown>)['_internal']).toBe(1);
        expect((p as Record<string, unknown>)['$ref']).toBe('r');
    });
});

describe('Deprecation warnings', () => {
    let warnSpy: jest.SpyInstance;

    beforeEach(() => {
        warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
        setDeprecationWarningsEnabled(true);
    });

    afterEach(() => {
        warnSpy.mockRestore();
        setDeprecationWarningsEnabled(true);
    });

    it('warns once when reading PascalCase the first time on a shape', () => {
        const p = wrapDualCasing({ WarnsOnce: 'X' });
        void (p as Record<string, unknown>)['WarnsOnce'];
        expect(warnSpy).toHaveBeenCalledTimes(1);
        expect(warnSpy.mock.calls[0][0]).toMatch(/PascalCase property access 'WarnsOnce'/);
        expect(warnSpy.mock.calls[0][0]).toMatch(/Use 'warnsOnce'/);
    });

    it('does NOT warn when reading camelCase', () => {
        const p = wrapDualCasing({ NoWarn: 'X' });
        void (p as Record<string, unknown>)['noWarn'];
        expect(warnSpy).not.toHaveBeenCalled();
    });

    it('warns only once for repeated PascalCase reads on the same shape', () => {
        const p = wrapDualCasing({ WarnsOnlyOnce: 'X' });
        void (p as Record<string, unknown>)['WarnsOnlyOnce'];
        void (p as Record<string, unknown>)['WarnsOnlyOnce'];
        void (p as Record<string, unknown>)['WarnsOnlyOnce'];
        expect(warnSpy).toHaveBeenCalledTimes(1);
    });

    it('warns only once across two different objects sharing the same shape', () => {
        const p1 = wrapDualCasing({ WarnsOnlyOnceSameShape: 'X' });
        const p2 = wrapDualCasing({ WarnsOnlyOnceSameShape: 'Y' });
        void (p1 as Record<string, unknown>)['WarnsOnlyOnceSameShape'];
        void (p2 as Record<string, unknown>)['WarnsOnlyOnceSameShape'];
        expect(warnSpy).toHaveBeenCalledTimes(1);
    });

    it('warns again when the shape (own-key set) differs', () => {
        const p1 = wrapDualCasing({ WarnsAgain: 'X' });
        const p2 = wrapDualCasing({ WarnsAgain: 'Y', Other: 1 });
        void (p1 as Record<string, unknown>)['WarnsAgain'];
        void (p2 as Record<string, unknown>)['WarnsAgain'];
        expect(warnSpy).toHaveBeenCalledTimes(2);
    });

    it('is silent when warnings are disabled', () => {
        setDeprecationWarningsEnabled(false);
        const p = wrapDualCasing({ SilentWarn: 'X' });
        void (p as Record<string, unknown>)['SilentWarn'];
        expect(warnSpy).not.toHaveBeenCalled();
    });

    it('resumes warning on a not-yet-warned (shape, prop) after re-enabling', () => {
        setDeprecationWarningsEnabled(false);
        const p1 = wrapDualCasing({ WarnsDelayed: 'X' });
        void (p1 as Record<string, unknown>)['WarnsDelayed'];
        setDeprecationWarningsEnabled(true);
        const p2 = wrapDualCasing({ WarnsDelayed: 'Y', Distinct: 1 });
        void (p2 as Record<string, unknown>)['WarnsDelayed'];
        expect(warnSpy).toHaveBeenCalledTimes(1);
    });
});

describe('Proxy set trap', () => {
    it('throws when assigning a casing variant of an existing key', () => {
        const p = wrapDualCasing({ Code: 'X' }) as Record<string, unknown>;
        expect(() => { p['code'] = 'Y'; }).toThrow(/Casing clash on insert/);
    });

    it('allows assigning a fresh property that does not clash', () => {
        const p = wrapDualCasing({ Code: 'X' }) as Record<string, unknown>;
        p['Other'] = 'Z';
        expect(p['Other']).toBe('Z');
        expect(p['other']).toBe('Z');
    });

    it('allows reassigning an existing key by its own casing', () => {
        const p = wrapDualCasing({ Code: 'X' }) as Record<string, unknown>;
        p['Code'] = 'Y';
        expect(p['Code']).toBe('Y');
    });

    it('passes through Symbol-keyed assignment without casing logic', () => {
        const sym = Symbol('marker');
        const p = wrapDualCasing({}) as Record<PropertyKey, unknown>;
        p[sym] = 1;
        expect(p[sym]).toBe(1);
    });

    it('passes through non-letter-prefixed assignment without clash check', () => {
        const p = wrapDualCasing({ _internal: 1 }) as Record<string, unknown>;
        p['_other'] = 2;
        p['$ref'] = 'r';
        p['1foo'] = 3;
        expect(p['_other']).toBe(2);
        expect(p['$ref']).toBe('r');
        expect(p['1foo']).toBe(3);
    });

    it('warns again on PascalCase read after a fresh property is added (shape change)', () => {
        const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
        try {
            setDeprecationWarningsEnabled(true);
            const p = wrapDualCasing({ Distinct1: 'X' }) as Record<string, unknown>;
            void p['Distinct1'];
            expect(warnSpy).toHaveBeenCalledTimes(1);
            p['Distinct2'] = 'Y';
            void p['Distinct1'];
            expect(warnSpy).toHaveBeenCalledTimes(2);
        } finally {
            warnSpy.mockRestore();
        }
    });

    it('invalidates shape cache when a non-letter-prefixed key is added', () => {
        const warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
        try {
            setDeprecationWarningsEnabled(true);
            const p = wrapDualCasing({ ShapeKey1: 'X' }) as Record<string, unknown>;
            void p['ShapeKey1'];
            expect(warnSpy).toHaveBeenCalledTimes(1);
            // Adding a non-letter-prefixed own key should invalidate the cached
            // shape signature on the target. After that, a fresh PascalCase read
            // recomputes a new signature ('ShapeKey1\x00_meta') and warns again
            // because the throttle map has no entry for the new signature.
            p['_meta'] = 1;
            void p['ShapeKey1'];
            expect(warnSpy).toHaveBeenCalledTimes(2);
        } finally {
            warnSpy.mockRestore();
        }
    });
});

describe('Proxy has trap', () => {
    it('returns true for either casing of an existing property', () => {
        const p = wrapDualCasing({ Code: 'X' });
        expect('Code' in p).toBe(true);
        expect('code' in p).toBe(true);
    });

    it('returns false for a property neither casing has', () => {
        const p = wrapDualCasing({ Code: 'X' });
        expect('missing' in p).toBe(false);
        expect('Missing' in p).toBe(false);
    });

    it('passes through non-letter-prefixed `in` checks unchanged', () => {
        const p = wrapDualCasing({ _internal: 1, $ref: 'r' } as Record<string, unknown>);
        expect('_internal' in p).toBe(true);
        expect('$ref' in p).toBe(true);
        expect('_missing' in p).toBe(false);
    });

    it('Object.keys returns only the actual stored key', () => {
        expect(Object.keys(wrapDualCasing({ Code: 'X' }))).toEqual(['Code']);
        expect(Object.keys(wrapDualCasing({ code: 'X' }))).toEqual(['code']);
    });

    it('JSON.stringify reflects storage only', () => {
        expect(JSON.stringify(wrapDualCasing({ Code: 'X' }))).toBe('{"Code":"X"}');
    });
});

describe('wrapDeep', () => {
    it('returns primitives unchanged', () => {
        expect(wrapDeep(42)).toBe(42);
        expect(wrapDeep('hello')).toBe('hello');
        expect(wrapDeep(null)).toBe(null);
        expect(wrapDeep(undefined)).toBe(undefined);
        expect(wrapDeep(true)).toBe(true);
    });

    it('wraps a plain object so dual-casing reads work', () => {
        const wrapped = wrapDeep({ Code: 'X' });
        expect((wrapped as Record<string, unknown>)['code']).toBe('X');
    });

    it('descends into nested plain objects (inlined struct case)', () => {
        const wrapped = wrapDeep({
            Coding: { AcousticCode: 1, VisualCode: 2 },
        }) as Record<string, unknown>;
        const coding = wrapped['coding'] as Record<string, unknown>;
        expect(coding['acousticCode']).toBe(1);
        expect(coding['visualCode']).toBe(2);
    });

    it('descends into arrays of plain objects', () => {
        const arr = wrapDeep([{ Code: 'A' }, { Code: 'B' }]) as Array<Record<string, unknown>>;
        expect(arr[0]['code']).toBe('A');
        expect(arr[1]['code']).toBe('B');
    });

    it('descends into Map values, leaving keys untouched', () => {
        const m = new Map<string, { Code: string }>();
        m.set('first', { Code: 'A' });
        const wrapped = wrapDeep(m);
        const first = wrapped.get('first') as Record<string, unknown>;
        expect(first['code']).toBe('A');
    });

    it('descends into Set values', () => {
        const s = new Set<{ Code: string }>();
        s.add({ Code: 'A' });
        const wrapped = wrapDeep(s);
        const first = wrapped.values().next().value as Record<string, unknown>;
        expect(first['code']).toBe('A');
    });

    it('is idempotent on already-wrapped subgraphs', () => {
        const inner = wrapDualCasing({ Code: 'A' });
        const outer = { Inner: inner };
        const wrapped = wrapDeep(outer) as Record<string, unknown>;
        expect((wrapped['inner'] as Record<string, unknown>)['code']).toBe('A');
        expect(wrapped['Inner']).toBe(inner);
    });

    it('preserves dual-casing access across cyclic back-pointers', () => {
        const garage: Record<string, unknown> = { CompanyName: 'Boite' };
        const employee: Record<string, unknown> = { Garage: garage };
        garage['Employees'] = [employee];
        const wrappedGarage = wrapDeep(garage) as Record<string, unknown>;
        expect(wrappedGarage['companyName']).toBe('Boite');
        const employees = wrappedGarage['employees'] as Array<Record<string, unknown>>;
        const e0 = employees[0];
        const garageBackref = e0['garage'] as Record<string, unknown>;
        expect(garageBackref['companyName']).toBe('Boite');
    });
});
