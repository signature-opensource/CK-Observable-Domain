import { ObservableDomain } from '@local/ck-gen';

if( process.env['VSCODE_INSPECTOR_OPTIONS'] ) jest.setTimeout(30 * 60 * 1000 );

// Covers the "observable domain singletons export" feature: the ObservableDomain client exposes a
// `singletons` Map<fullTypeName, object> built from the "S" property of a domain export and kept in
// sync with "N" (new object) and "D" (delete) events.
describe('ObservableDomain singletons', () => {

    const exportWithSingleton = '{"N":1,"C":2,"P":["Code"],"O":[{"þ":[0,"A"]},{"°":1,"Code":"E001"}],"R":[0],"S":[["My.App.RootSingleton",0]]}';

    it('populates the singletons map from a non-empty "S" in a domain export', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportWithSingleton));

        expect(o.singletons.size).toBe(1);
        // The map points to the very object referenced by "S" (here the root at graph index 0).
        expect(o.singletons.get('My.App.RootSingleton')).toBe(o.roots[0]);
        expect((o.singletons.get('My.App.RootSingleton') as Record<string, unknown>)['Code']).toBe('E001');
    });

    it('exposes an empty singletons map when the export has no singletons', () => {
        const initial = '{"N":1,"C":1,"P":[],"O":[{"þ":[0,"A"]}],"R":[],"S":[]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));

        expect(o.singletons.size).toBe(0);
    });

    it('populates the singletons map when constructed directly from an export string', () => {
        const o = new ObservableDomain(exportWithSingleton);

        expect(o.singletons.size).toBe(1);
        expect(o.singletons.get('My.App.RootSingleton')).toBe(o.roots[0]);
    });

    it('registers a singleton created by a New-object event carrying its full type name', () => {
        const initial = '{"N":1,"C":1,"P":[],"O":[{"þ":[0,"A"]}],"R":[],"S":[]}';
        const create = '{"N":2,"E":[[["N",0,"","My.App.NewSingleton"]]]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        expect(o.singletons.size).toBe(0);

        o.applyWatchEvent(JSON.parse(create));

        expect(o.singletons.size).toBe(1);
        expect(o.singletons.has('My.App.NewSingleton')).toBe(true);
    });

    it('removes a singleton (populated from an export) when its object is deleted', () => {
        const del = '{"N":2,"E":[[["D",0]]]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportWithSingleton));
        expect(o.singletons.has('My.App.RootSingleton')).toBe(true);

        o.applyWatchEvent(JSON.parse(del));

        expect(o.singletons.has('My.App.RootSingleton')).toBe(false);
        expect(o.singletons.size).toBe(0);
    });

    it('removes a singleton (created by a New-object event) when it is later deleted', () => {
        const initial = '{"N":1,"C":1,"P":[],"O":[{"þ":[0,"A"]}],"R":[],"S":[]}';
        const create = '{"N":2,"E":[[["N",0,"","My.App.NewSingleton"]]]}';
        const del = '{"N":3,"E":[[["D",0]]]}';
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(initial));
        o.applyWatchEvent(JSON.parse(create));
        expect(o.singletons.has('My.App.NewSingleton')).toBe(true);

        o.applyWatchEvent(JSON.parse(del));

        expect(o.singletons.has('My.App.NewSingleton')).toBe(false);
        expect(o.singletons.size).toBe(0);
    });

    it('removes a singleton (populated via the constructor) when its object is deleted', () => {
        const del = '{"N":2,"E":[[["D",0]]]}';
        const o = new ObservableDomain(exportWithSingleton);
        expect(o.singletons.has('My.App.RootSingleton')).toBe(true);

        o.applyWatchEvent(JSON.parse(del));

        expect(o.singletons.has('My.App.RootSingleton')).toBe(false);
        expect(o.singletons.size).toBe(0);
    });

    it('clears the singletons map on an empty watch event', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportWithSingleton));
        expect(o.singletons.size).toBe(1);

        const warn = jest.spyOn(console, 'warn').mockImplementation(() => {});
        try {
            o.applyWatchEvent('');
        } finally {
            warn.mockRestore();
        }

        expect(o.singletons.size).toBe(0);
    });
});
