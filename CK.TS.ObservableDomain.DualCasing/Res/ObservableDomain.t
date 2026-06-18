create <ts> transformer on "CK/ObservableDomain/ObservableDomain.ts"
begin
    ensure import { wrapDeep, wrapDualCasing } from "./DualCasingProxy";

    insert """

                for (let i = 0; i < this._graph.length; i++) {
                    this._graph[i] = wrapDeep(this._graph[i]);
                }
    """
        before single "this._roots = o.R.map(i => this._graph[i]);";

    insert """

            for (let i = 0; i < this._graph.length; i++) {
                this._graph[i] = wrapDeep(this._graph[i]);
            }
    """
        before single "this._roots.splice(0, this._roots.length);";

    replace single "newOne = {};" with "newOne = wrapDualCasing({});";

    replace single """
            if (ref !== undefined) return this._graph[ref];
        }
        return o;
    """ with """
            if (ref !== undefined) return this._graph[ref];
        }
        return wrapDeep(o);
    """;
end
