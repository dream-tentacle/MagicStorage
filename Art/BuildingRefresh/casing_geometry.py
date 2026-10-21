"""Shared hard casing geometry; adjacent bevels use identical vertices."""
import math


def polygon(points):
    return 'M'+'L'.join(f'{x:g} {y:g}' for x,y in points)+'Z'


def corners(x,y,w,h,cut):
    return [(x+cut,y),(x+w-cut,y),(x+w,y+cut),(x+w,y+h-cut),
            (x+w-cut,y+h),(x+cut,y+h),(x,y+h-cut),(x,y+cut)]


def chamfered_outline(x,y,w,h,cut=10):
    return polygon(corners(x,y,w,h,cut))


def casing_faces(x,y,w,h,depth=26,cut=10,bevel=8):
    """Full side silhouette, top plate and connected planar edge bevels."""
    outer=corners(x,y,w,h-depth,cut)
    edges=list(zip(outer,outer[1:]+outer[:1]))
    shifted=[]
    for p,q in edges:
        dx,dy=q[0]-p[0],q[1]-p[1]
        length=math.hypot(dx,dy)
        shifted.append(((p[0]-dy/length*bevel,p[1]+dx/length*bevel),(dx,dy)))
    inner=[]
    for (p,v),(q,u) in zip(shifted[-1:]+shifted[:-1],shifted):
        cross=v[0]*u[1]-v[1]*u[0]
        t=((q[0]-p[0])*u[1]-(q[1]-p[1])*u[0])/cross
        inner.append((p[0]+t*v[0],p[1]+t*v[1]))
    d=chamfered_outline(x,y,w,h,cut)
    faces=[(d,'side'),(polygon(outer),'shell')]
    for i,(p,q) in enumerate(edges):
        color='light' if i in [0,6,7] else 'bevel'
        faces.append((polygon([p,q,inner[(i+1)%8],inner[i]]),color))
    return d,faces
