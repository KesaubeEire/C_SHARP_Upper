/** nodes7 无 TS 声明文件，显式声明 */
declare module 'nodes7' {
  interface InitiateConnectionOpts {
    host: string
    port: number
    rack: number
    slot: number
    timeout: number
    localTSAP: number
    remoteTSAP: number
  }

  class NodeS7 {
    constructor(opts?: { silent?: boolean })
    initiateConnection(opts: InitiateConnectionOpts, callback: (err: any) => void): void
    dropConnection(): void
    addItems(tags: string[]): void
    removeItems(tags: string[]): void
    readAllItems(callback: (err: any, values: Record<string, any>) => void): void
    writeItems(tag: string, value: any, callback: (err: any) => void): void
    writeItems(tags: string[], values: any[], callback: (err: any) => void): void
    setTranslationCB(cb: (tag: string) => string): void
    translationCB: (tag: string) => string
  }

  export default NodeS7
}
