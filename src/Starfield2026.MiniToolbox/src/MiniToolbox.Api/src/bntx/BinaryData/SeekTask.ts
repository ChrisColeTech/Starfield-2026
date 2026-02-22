/**
 * 1:1 port of SeekTask.cs
 * Temporarily moves the stream position and restores it when disposed.
 */
export interface Seekable {
    position: number;
}

export class SeekTask {
    private _stream: Seekable;
    private _oldPosition: number;

    constructor(stream: Seekable, offset: number, origin: 'begin' | 'current' | 'end') {
        this._stream = stream;
        this._oldPosition = stream.position;
        switch (origin) {
            case 'begin':
                stream.position = offset;
                break;
            case 'current':
                stream.position += offset;
                break;
            case 'end':
                stream.position = stream.position + offset;
                break;
        }
    }

    dispose(): void {
        this._stream.position = this._oldPosition;
    }

    /**
     * Execute an action within a temporary seek, then restore position.
     */
    static run<T>(stream: Seekable, offset: number, origin: 'begin' | 'current' | 'end', action: () => T): T {
        const task = new SeekTask(stream, offset, origin);
        try {
            return action();
        } finally {
            task.dispose();
        }
    }
}
