// SVG fill contours for native icon fonts. Curves are flattened below 0.04 source units.
const point=(x,y)=>[x,y];
const mid=(a,b)=>[(a[0]+b[0])/2,(a[1]+b[1])/2];
const cross=(a,b,c)=>Math.abs((b[0]-a[0])*(a[1]-c[1])-(a[0]-c[0])*(b[1]-a[1]));
function cubic(a,b,c,d,out,depth=0) {
 const length=Math.hypot(d[0]-a[0],d[1]-a[1]);
 if(depth>=14||(length>0&&Math.max(cross(a,d,b),cross(a,d,c))/length<.04)) {out.push(d);return;}
 const ab=mid(a,b),bc=mid(b,c),cd=mid(c,d),abc=mid(ab,bc),bcd=mid(bc,cd),center=mid(abc,bcd);
 cubic(a,ab,abc,center,out,depth+1);cubic(center,bcd,cd,d,out,depth+1);
}
function arc(start,rx,ry,rotation,large,sweep,end,out) {
 rx=Math.abs(rx);ry=Math.abs(ry);
 if(!rx||!ry||(start[0]===end[0]&&start[1]===end[1])) {out.push(end);return;}
 if(![0,1].includes(large)||![0,1].includes(sweep))throw Error('Invalid SVG arc flags');
 const phi=rotation*Math.PI/180,cos=Math.cos(phi),sin=Math.sin(phi);
 const dx=(start[0]-end[0])/2,dy=(start[1]-end[1])/2,x=cos*dx+sin*dy,y=-sin*dx+cos*dy;
 const scale=x*x/(rx*rx)+y*y/(ry*ry);if(scale>1){rx*=Math.sqrt(scale);ry*=Math.sqrt(scale);}
 const denominator=rx*rx*y*y+ry*ry*x*x;
 const factor=(large===sweep?-1:1)*Math.sqrt(Math.max(0,(rx*rx*ry*ry-denominator)/denominator));
 const cx=factor*rx*y/ry,cy=-factor*ry*x/rx;
 const center=[cos*cx-sin*cy+(start[0]+end[0])/2,sin*cx+cos*cy+(start[1]+end[1])/2];
 const ux=(x-cx)/rx,uy=(y-cy)/ry,vx=(-x-cx)/rx,vy=(-y-cy)/ry;
 const begin=Math.atan2(uy,ux);let delta=Math.atan2(ux*vy-uy*vx,ux*vx+uy*vy);
 if(!sweep&&delta>0)delta-=2*Math.PI;if(sweep&&delta<0)delta+=2*Math.PI;
 const steps=Math.max(1,Math.ceil(Math.abs(delta)/(Math.PI/32)));
 for(let i=1;i<=steps;i++){const t=begin+delta*i/steps;out.push([center[0]+cos*rx*Math.cos(t)-sin*ry*Math.sin(t),center[1]+sin*rx*Math.cos(t)+cos*ry*Math.sin(t)]);}
 out[out.length-1]=end;
}
function pathContours(data) {
 const tokenPattern=/[a-zA-Z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?/g;
 const tokens=data.match(tokenPattern)??[];
 if(data.replace(tokenPattern,'').replace(/[\s,]/g,''))throw Error('Invalid SVG path input');
 const result=[];let i=0,cmd='',previous='',p=[0,0],start=p,points=[],control=null;
 const number=()=>{const t=tokens[i++];if(t===undefined||!/^[+\-.\d]/.test(t)||!Number.isFinite(Number(t)))throw Error('Invalid SVG coordinate');return Number(t)};
 const finish=()=>{if(points.length>1&&points.at(-1).every((v,j)=>Math.abs(v-points[0][j])<1e-8))points.pop();if(points.length>=3)result.push(points);points=[];};
 while(i<tokens.length) {
  if(/^[a-z]$/i.test(tokens[i]))cmd=tokens[i++];
  if(!cmd)throw Error('SVG path command missing');
  const upper=cmd.toUpperCase(),relative=cmd!==upper,base=p;
  const pair=()=>{const x=number(),y=number();return relative?[base[0]+x,base[1]+y]:[x,y]};
  if(upper==='Z'){p=start;finish();cmd='';control=null;previous=upper;continue;}
  let end,nextControl=null;
  switch(upper){
   case 'M':finish();p=pair();start=p;points=[p];cmd=relative?'l':'L';break;
   case 'L':p=pair();points.push(p);break;
   case 'H':p=[number()+(relative?p[0]:0),p[1]];points.push(p);break;
   case 'V':p=[p[0],number()+(relative?p[1]:0)];points.push(p);break;
   case 'C':{const a=pair(),b=pair();end=pair();cubic(p,a,b,end,points);p=end;nextControl=b;break;}
   case 'S':{const a=['C','S'].includes(previous)&&control?[2*p[0]-control[0],2*p[1]-control[1]]:p,b=pair();end=pair();cubic(p,a,b,end,points);p=end;nextControl=b;break;}
   case 'Q':case 'T':{const b=upper==='Q'?pair():['Q','T'].includes(previous)&&control?[2*p[0]-control[0],2*p[1]-control[1]]:p;end=pair();cubic(p,[p[0]+2*(b[0]-p[0])/3,p[1]+2*(b[1]-p[1])/3],[end[0]+2*(b[0]-end[0])/3,end[1]+2*(b[1]-end[1])/3],end,points);p=end;nextControl=b;break;}
   case 'A':{const rx=number(),ry=number(),rotation=number(),large=number(),sweep=number();end=pair();arc(p,rx,ry,rotation,large,sweep,end,points);p=end;break;}
   default:throw Error('Unsupported SVG command '+cmd);
  }
  control=nextControl;previous=upper;
 }
 finish();return result;
}
export function svgContours(svg) {
 if(/<script|<image|<use|<foreignObject|\bon\w+\s*=|href\s*=|\btransform\s*=|\bstroke\s*=|fill-rule\s*=\s*["']evenodd/i.test(svg))throw Error('Unsupported or unsafe SVG drawing');
 const view=svg.match(/\bviewBox="([^"]+)"/i)?.[1].trim().split(/[\s,]+/).map(Number);
 if(!view||view.length!==4||view.some(n=>!Number.isFinite(n))||view[2]<=0||view[3]<=0)throw Error('SVG viewBox missing/invalid');
 if(/<(?:rect|circle|ellipse|polygon|polyline|line|g)\b/i.test(svg))throw Error('Only baked fill paths are supported');
 const paths=[...svg.matchAll(/<path\b[^>]*\bd="([^"]+)"[^>]*\/?\s*>/gi)];
 if(!paths.length)throw Error('SVG fill paths missing');
 const scale=24/Math.max(view[2],view[3]),offset=[(24-view[2]*scale)/2,(24-view[3]*scale)/2];
 return paths.flatMap(m=>pathContours(m[1])).map(p=>p.map(([x,y])=>point((x-view[0])*scale+offset[0],(y-view[1])*scale+offset[1])));
}
