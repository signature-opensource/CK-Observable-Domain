import {
    ObservableDomain,
    ObservableDomainClient,
    IObservableDomainConnection,
    wrapDualCasing,
    setDeprecationWarningsEnabled,
    deserialize,
} from '@local/ck-gen';

if( process.env['VSCODE_INSPECTOR_OPTIONS'] ) jest.setTimeout(30 * 60 * 1000 );

describe('ObservableDomain — dual-casing graph', () => {
    let warnSpy: jest.SpyInstance;

    beforeEach(() => {
        warnSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
        setDeprecationWarningsEnabled(true);
    });

    afterEach(() => {
        warnSpy.mockRestore();
        setDeprecationWarningsEnabled(true);
    });

    it('exposes camelCase access to a PascalCase domain export', () => {
        const initial = '{"N":1,"P":["Code","SourceComponent"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"E001","SourceComponent":"motor"}],"R":[0],"S":[]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        const root = o.roots[0] as Record<string, unknown>;
        expect(root['code']).toBe('E001');
        expect(root['sourceComponent']).toBe('motor');
        expect(warnSpy).not.toHaveBeenCalled();
    });

    it('warns once on PascalCase access and aliases to the same value', () => {
        const initial = '{"N":1,"P":["Code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"E001"}],"R":[0],"S":[]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        const root = o.roots[0] as Record<string, unknown>;
        expect(root['Code']).toBe('E001');
        expect(root['Code']).toBe('E001');
        expect(warnSpy).toHaveBeenCalledTimes(1);
    });

    it('throws when applyDomainExport receives an object with both casings', () => {
        const bad = '{"N":1,"P":["Code","code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"X","code":"Y"}],"R":[0],"S":[]}';
        const o = new ObservableDomain();
        expect(() => o.applyWatchEvent(JSON.parse(bad))).toThrow(/Casing clash at wrap time/);
    });

    it('deep-wraps inlined struct values nested in a graph object', () => {
        const initial = '{"N":1,"P":["Coding"],"O":[{"þ":[0,"A"]},{"°":1,"Coding":{"AcousticCode":7,"VisualCode":3}}],"R":[0],"S":[]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        const root = o.roots[0] as Record<string, unknown>;
        const coding = root['coding'] as Record<string, unknown>;
        expect(coding['acousticCode']).toBe(7);
        expect(coding['visualCode']).toBe(3);
    });

    it('wraps inline plain objects assigned via a "C" event', () => {
        const initial = '{"N":1,"P":["Coding"],"O":[{"þ":[0,"A"]},{"°":1,"Coding":null}],"R":[0],"S":[]}';
        const update = '{"N":2,"E":[[["C",0,0,{"AcousticCode":4,"VisualCode":5}]]]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        o.applyWatchEvent(JSON.parse(update));
        const root = o.roots[0] as Record<string, unknown>;
        const coding = root['coding'] as Record<string, unknown>;
        expect(coding['acousticCode']).toBe(4);
        expect(coding['visualCode']).toBe(5);
    });

    it('throws when a "C" event introduces a clashing-casing property', () => {
        const initial = '{"N":1,"P":["Code","code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"X"}],"R":[0],"S":[]}';
        const update = '{"N":2,"E":[[["C",0,1,"Y"]]]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        expect(() => o.applyWatchEvent(JSON.parse(update))).toThrow(/Casing clash on insert/);
    });

    it('wraps graph entries when constructed with initialState (string form)', () => {
        const initial = '{"N":1,"P":["Code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"E001"}],"R":[0],"S":[]}';
        const o = new ObservableDomain(initial);
        const root = o.roots[0] as Record<string, unknown>;
        expect(root['code']).toBe('E001');
    });

    it('wraps graph entries when constructed with initialState (DomainExportEvent form)', () => {
        const initial = '{"N":1,"P":["Code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"E001"}],"R":[0],"S":[]}';
        const event = deserialize(initial, { prefix: '' });
        const o = new ObservableDomain(event);
        const root = o.roots[0] as Record<string, unknown>;
        expect(root['code']).toBe('E001');
    });

    it('keys a map by the graph proxies, so the watch events update it in place', () => {
        // The graph: carl (0), dora (1), and a map (2) keyed by them, carl already in it.
        const initial = '{"N":1,"P":["Name"],"O":[{"þ":[0,"A"]},{"°":1,"Name":"carl"},{"°":2,"Name":"dora"},[{"þ":[3,"M"]},[{">":1},"Magenta"]]],"R":[],"S":[]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        const colours = [...o.allObjects].find((x) => x instanceof Map) as Map<Record<string, unknown>, string>;
        const [carl] = [...colours.keys()];
        expect(carl['name']).toBe('carl');

        // carl changes colour, dora gets one then loses it: the events name the keys by their graph entries.
        o.applyWatchEvent(JSON.parse('{"N":2,"E":[[["M",2,{"=":0},"Green"],["M",2,{"=":1},"Blue"],["K",2,{"=":1}]]]}'));

        expect([...colours.entries()].map(([k, v]) => [k['name'], v])).toEqual([['carl', 'Green']]);
    });

    it('honors ObservableDomainClient suppressDeprecationWarnings flag', () => {
        const noopConnection: IObservableDomainConnection = {
            startAsync: async () => true,
            startListeningAsync: async () => ({}),
            onMessage: () => {},
            onClose: () => {},
            stopAsync: async () => {},
        };

        setDeprecationWarningsEnabled(true);
        new ObservableDomainClient(noopConnection, { suppressDeprecationWarnings: true });

        const probe = wrapDualCasing({ DistinctSuppressedKey: 'Z' }) as Record<string, unknown>;
        void probe['DistinctSuppressedKey'];
        expect(warnSpy).not.toHaveBeenCalled();

        setDeprecationWarningsEnabled(true);
    });
});
