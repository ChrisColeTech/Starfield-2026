import * as path from 'path';

export class PathString {
    private rootPath: string;

    constructor(root: string) {
        this.rootPath = path.dirname(root);
    }

    combine(str: string): string {
        return path.join(this.rootPath, str);
    }
}
