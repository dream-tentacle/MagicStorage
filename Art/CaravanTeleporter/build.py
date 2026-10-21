"""Original SVG study: a restrained storage-family terminal with a deep-blue vortex."""
from pathlib import Path
from math import sin, cos, pi, hypot
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
START = -.32


def spiral(t):
    theta = -.85 + 2*pi*1.72*t
    radius = 132*(1-t)**.91 + 4
    return radius*cos(theta), radius*sin(theta)


def edges(t):
    x,y = spiral(t)
    before,after = max(START,t-.0001),min(1,t+.0001)
    ax,ay = spiral(before)
    bx,by = spiral(after)
    dx,dy = bx-ax,by-ay
    length = hypot(dx,dy)
    half = 12.5*(1-t)**.72+1.5
    return (x-dy/length*half,y+dx/length*half),(x+dy/length*half,y-dx/length*half)


def path(points, closed=False):
    return 'M'+'L'.join(f'{x:.3f} {y:.3f}' for x,y in points)+('Z' if closed else '')


# Preserve the previous gradient coordinates while extending only the outer tail.
original_points = [point for i in range(421) for point in edges(i/420)]
gx = min(point[0] for point in original_points)
gy = min(point[1] for point in original_points)
gx2 = gx+.88*(max(point[0] for point in original_points)-gx)
gy2 = max(point[1] for point in original_points)
times = [START+(1-START)*i/560 for i in range(561)]
samples = [edges(t) for t in times]
# The entire straight closing edge must be outside the visible aperture.
a,b = samples[0]
dx,dy = b[0]-a[0],b[1]-a[1]
projection = max(0,min(1,-(a[0]*dx+a[1]*dy)/(dx*dx+dy*dy)))
assert hypot(a[0]+projection*dx,a[1]+projection*dy)>146
ribbon = path([p[0] for p in samples]+[p[1] for p in reversed(samples)],True)
glint = path([p[0] for t,p in zip(times,samples) if t<335/420])
svg = f'''<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
  <title>魔法存储 — 远行队传送器，第二稿</title>
  <desc>Original 1×1 fixed-orientation caravan teleporter. A deep-blue continuous spiral curls into a dark transfer aperture, surrounded by a thin metal collar on a low pearl-grey rounded-square plinth. Restrained RGB(197,229,245) accents; uniform pure-black outer contour.</desc>
  <defs>
    <radialGradient id="portal-field" cx="47%" cy="50%" r="65%">
      <stop offset="0" stop-color="#091a34"/>
      <stop offset=".54" stop-color="#122f52"/>
      <stop offset="1" stop-color="#204570"/>
    </radialGradient>
    <linearGradient id="spiral-flow" gradientUnits="userSpaceOnUse" x1="{gx:.3f}" y1="{gy:.3f}" x2="{gx2:.3f}" y2="{gy2:.3f}">
      <stop offset="0" stop-color="#3c6e9d"/>
      <stop offset=".43" stop-color="#2d5787"/>
      <stop offset="1" stop-color="#1e426f"/>
    </linearGradient>
    <clipPath id="aperture"><ellipse cx="256" cy="264" rx="145" ry="135"/></clipPath>
  </defs>

  <!-- Low casing shares the existing terminal construction. -->
  <rect x="26" y="72" width="460" height="414" rx="28" fill="#798893"/>
  <path d="M29 449Q29 480 58 482H455Q482 480 482 449" fill="none" stroke="#52616c" stroke-width="6"/>
  <rect x="26" y="42" width="460" height="416" rx="28" fill="#d8dfe3"/>
  <path d="M26 430Q26 458 54 458H458Q486 458 486 430" fill="none" stroke="#25313c" stroke-width="4"/>
  <path d="M40 200V76Q40 56 61 56H450Q468 56 472 72" fill="none" stroke="#f2f5f6" stroke-width="3" stroke-linecap="round"/>
  <path d="M473 85V418Q473 444 446 444H63" fill="none" stroke="#a8b6bf" stroke-width="4" stroke-linecap="round"/>

  <!-- One quiet controller leaves the vortex visually dominant. -->
  <rect x="193" y="64" width="126" height="29" rx="5" fill="#aebdc6"/>
  <rect x="197" y="68" width="118" height="21" rx="3" fill="#344a5b"/>
  <path d="M209 78H232M242 78H265" stroke="#c5e5f5" stroke-width="3"/>
  <path d="M284 78H302" stroke="#91afc3" stroke-width="2"/>

  <!-- A narrow metal collar frames the portal without a bulky door rim. -->
  <ellipse cx="256" cy="274" rx="163" ry="151" fill="#8ca2b2"/>
  <ellipse cx="256" cy="264" rx="163" ry="151" fill="#bfced9"/>
  <ellipse cx="256" cy="264" rx="155" ry="144" fill="#e4ebef"/>
  <ellipse cx="256" cy="264" rx="147" ry="137" fill="#708da5"/>
  <ellipse cx="256" cy="264" rx="145" ry="135" fill="url(#portal-field)"/>
  <path d="M118 192A159 148 0 0 1 213 121" fill="none" stroke="#f5f8fa" stroke-width="3" stroke-linecap="round"/>
  <path d="M325 129A159 148 0 0 1 402 204M186 399A159 148 0 0 1 112 327" fill="none" stroke="#c5e5f5" stroke-width="5" stroke-linecap="round"/>

  <!-- The outer spiral and its highlight continue beyond the aperture and are occluded by its rim. -->
  <g clip-path="url(#aperture)">
    <g transform="translate(256 264) scale(1 .93)">
      <path d="{ribbon}" fill="url(#spiral-flow)"/>
      <path d="{glint}" fill="none" stroke="#6994b9" stroke-opacity=".42" stroke-width="1.5" stroke-linecap="round"/>
    </g>
  </g>

  <!-- Two slim field couplers; the corners remain clear. -->
  <rect x="83" y="237" width="23" height="57" rx="5" fill="#9eb2c0"/>
  <rect x="85" y="233" width="21" height="53" rx="4" fill="#dce6ec"/>
  <rect x="93" y="246" width="7" height="26" rx="2" fill="#c5e5f5"/>
  <rect x="406" y="237" width="23" height="57" rx="5" fill="#9eb2c0"/>
  <rect x="406" y="233" width="21" height="53" rx="4" fill="#dce6ec"/>
  <rect x="412" y="246" width="7" height="26" rx="2" fill="#c5e5f5"/>

  <rect x="204" y="468" width="105" height="7" rx="3.5" fill="#344756"/>
  <path d="M217 471H243M277 471H296" stroke="#c5e5f5" stroke-width="2" stroke-linecap="round"/>
  <!-- Black is reserved for the uniform exterior outline. -->
  <path d="M54 42H458Q486 42 486 70V458Q486 486 458 486H54Q26 486 26 458V70Q26 42 54 42Z" fill="none" stroke="#000000" stroke-width="8"/>
</svg>
'''
ET.fromstring(svg)
destination = root/'CaravanTeleporter-v2.svg'
destination.write_text(svg,encoding='utf-8')
print(destination)
