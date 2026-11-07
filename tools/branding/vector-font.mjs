// Deterministic TrueType transport for locally licensed contours. No artwork lives in this module.
export function vectorFont(icons, family='Math Solver Licensed Actions') {
if (!icons.length || icons.some((icon,i)=>icon.code!==icons[0].code+i)) throw new Error('Glyph codes must be contiguous and ordered');
const u16 = n => {const b=Buffer.alloc(2);b.writeUInt16BE(n&65535);return b;};
const i16 = n => {const b=Buffer.alloc(2);b.writeInt16BE(n);return b;};
const u32 = n => {const b=Buffer.alloc(4);b.writeUInt32BE(n>>>0);return b;};
const cat = (...b) => Buffer.concat(b.flat());
const pad = b => cat(b,Buffer.alloc((4-b.length%4)%4));
const checksum = b => {b=pad(b);let n=0;for(let i=0;i<b.length;i+=4)n=(n+b.readUInt32BE(i))>>>0;return n;};
const glyphs = [[] , ...icons.map(i=>i.contours.map(p=>p.map(([x,y])=>[Math.round(x*1024/24),Math.round((24-y)*1024/24)])))];
const glyphData=glyphs.map(contours=>{
  if(!contours.length) return Buffer.alloc(0);
  const points=contours.flat(),xs=points.map(p=>p[0]),ys=points.map(p=>p[1]);
  let end=-1,px=0,py=0;
  const dx=points.map(p=>{const d=p[0]-px;px=p[0];return i16(d);});
  const dy=points.map(p=>{const d=p[1]-py;py=p[1];return i16(d);});
  return pad(cat(i16(contours.length),i16(Math.min(...xs)),i16(Math.min(...ys)),i16(Math.max(...xs)),i16(Math.max(...ys)),contours.map(p=>u16(end+=p.length)),u16(0),Buffer.alloc(points.length,1),dx,dy));
});
let offset=0;
const loca=cat(...glyphData.map(g=>{const n=u32(offset);offset+=g.length;return n;}),u32(offset));
const glyphCount=glyphs.length;
const head=cat(u32(0x10000),u32(0x10000),u32(0),u32(0x5f0f3cf5),u16(3),u16(1024),Buffer.alloc(16),i16(0),i16(0),i16(1024),i16(1024),u16(0),u16(8),i16(2),i16(1),i16(0));
const hhea=cat(u32(0x10000),i16(1024),i16(0),i16(0),u16(1024),i16(0),i16(0),i16(1024),i16(1),i16(0),i16(0),Buffer.alloc(8),i16(0),u16(glyphCount));
const maxp=cat(u32(0x10000),u16(glyphCount),u16(Math.max(...glyphs.map(g=>g.flat().length))),u16(Math.max(...glyphs.map(g=>g.length))),u16(0),u16(0),u16(2),Buffer.alloc(16));
const os2=Buffer.alloc(96);os2.writeUInt16BE(4,0);os2.writeInt16BE(1024,2);os2.writeUInt16BE(400,4);os2.writeUInt16BE(5,6);
os2.writeUInt32BE(0x10000000,46);os2.write('MSOL',58,'ascii');os2.writeUInt16BE(0x40,62);os2.writeUInt16BE(icons[0].code,64);os2.writeUInt16BE(icons.at(-1).code,66);
os2.writeInt16BE(1024,68);os2.writeUInt16BE(1024,74);os2.writeUInt16BE(3,94);
const segCount=2, first=icons[0].code, last=icons.at(-1).code;
const format4=cat(u16(4),u16(32),u16(0),u16(segCount*2),u16(4),u16(1),u16(0),u16(last),u16(65535),u16(0),u16(first),u16(65535),u16(1-first),u16(1),u16(0),u16(0));
const cmap=cat(u16(0),u16(1),u16(3),u16(1),u32(12),format4);
const names=['Icons8 artwork; Icons8 Universal Multimedia License',family,'Regular',family.replaceAll(' ','')+'-1.0',family,'Version 1.0',family.replaceAll(' ',''),'https://icons8.com/license'];
const nameIds=[0,1,2,3,4,5,6,13];let nameOffset=0;
const nameStrings=names.map(s=>{const b=Buffer.from(s,'utf16le');b.swap16();return b;});
const name=cat(u16(0),u16(names.length),u16(6+12*names.length),nameStrings.map((s,i)=>{const r=cat(u16(3),u16(1),u16(0x409),u16(nameIds[i]),u16(s.length),u16(nameOffset));nameOffset+=s.length;return r;}),nameStrings);
const post=cat(u32(0x30000),u32(0),i16(-100),i16(50),u32(0),Buffer.alloc(16));
const tables=Object.entries({head,hhea,maxp,'OS/2':os2,cmap,glyf:cat(glyphData),loca,hmtx:cat(glyphs.map(g=>cat(u16(1024),i16(g.length ? Math.min(...g.flat().map(p=>p[0])) : 0)))),name,post}).sort(([a],[b])=>a<b?-1:a>b?1:0);
const power=2**Math.floor(Math.log2(tables.length));offset=12+16*tables.length;
const records=tables.map(([tag,data])=>{const r={tag,data,offset};offset+=pad(data).length;return r;});
const font=cat(u32(0x10000),u16(tables.length),u16(power*16),u16(Math.log2(power)),u16(tables.length*16-power*16),records.map(r=>cat(Buffer.from(r.tag),u32(checksum(r.data)),u32(r.offset),u32(r.data.length))),records.map(r=>pad(r.data)));
font.writeUInt32BE((0xb1b0afba-checksum(font))>>>0,records.find(r=>r.tag==='head').offset+8);
return font;
}
