import { ObservableDomain } from '@local/ck-gen';

// The object 0 is an empty ObservableList (a typed array "A"). The "I" event inserts an item in it.
const exportAt3 = '{"N":3,"P":[],"O":[[{"þ":[0,"A"]}]],"R":[],"S":[],"C":1}';

function list(o: ObservableDomain): any[] {
    return Array.from(o.allObjects)[0];
}

describe('ObservableDomain watch events - ', () => {

    it('a push that is not after the export throws', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportAt3));
        // The push of n°3 can arrive after the export of n°3 (the watcher subscribes to the pushes before the export),
        // but the domain can also have gone back to n°2: the client cannot know, it throws and its owner reloads it.
        expect(() => o.applyWatchEvent(JSON.parse('{"N":3,"E":[[["I",0,0,"Already in the export?"]]],"L":2}'))).toThrow();
    });

    it('a push without events advances the transaction number', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportAt3));
        o.applyWatchEvent(JSON.parse('{"N":4,"E":[[]],"L":3}'));
        expect(o.transactionNumber).toBe(4);
        o.applyWatchEvent(JSON.parse('{"N":5,"E":[[["I",0,0,"One"]]],"L":4}'));
        expect(o.transactionNumber).toBe(5);
        expect(list(o)).toEqual(["One"]);
    });

    it('an old push after an applied event still throws', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportAt3));
        o.applyWatchEvent(JSON.parse('{"N":4,"E":[[["I",0,0,"One"]]],"L":3}'));
        // The domain went back (a dangerous rollback or a reload of an older snapshot):
        // the client must not ignore it, it must resynchronize.
        expect(() => o.applyWatchEvent(JSON.parse('{"N":4,"E":[[["I",0,0,"Two"]]],"L":3}'))).toThrow();
    });

    it('a push after missed transactions still throws', () => {
        const o = new ObservableDomain();
        o.applyWatchEvent(JSON.parse(exportAt3));
        o.applyWatchEvent(JSON.parse('{"N":4,"E":[[["I",0,0,"One"]]],"L":3}'));
        expect(() => o.applyWatchEvent(JSON.parse('{"N":7,"E":[[["I",0,0,"Two"]]],"L":6}'))).toThrow();
    });
});
