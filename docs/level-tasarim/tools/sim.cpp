// PixelGame level simulator (scratchpad).
// Rules mirrored from CollectionOrder.cs / ShipQueuePool.cs / ShipDispatcher.cs:
//  - ship i of the sequence goes to lane i % cols; each lane is a FIFO, the first `rows` ships are visible
//  - only the front ship of a lane can be sent; a linked pair goes together and needs 2 empty slots,
//    the partner must be visible and have only its own group ahead of it
//  - docked ships take accessible cubes of their colour (4-neighbour touches outside air),
//    pick order: bottom row first, then closest to the row's ends, left first, then x
//  - a full ship departs; a full LINKED ship waits until its partner is full, then both depart
//  - fail when nothing can be sent and nothing can be collected
// Usage: sim <level.txt> <runs> [seed] [dfs 0|1|p] [noise n|m]
//   prints "random smart dfs"; 'p' = point-of-no-return report; noise m = taps while sailors still collect
#include <bits/stdc++.h>
using namespace std;

int W, H, COLS, ROWS, SLOTS;
vector<int> grid0;  // y*W+x, y=0 bottom row (Unity GridY)
struct Ship { int color, cap, link, hidden; };
vector<Ship> ships;
vector<int> partnerCache;
mt19937 rng;
int NOISE = 0;

struct State {
    vector<int8_t> occ;
    vector<uint8_t> air;
    vector<vector<int>> lanes;
    vector<int> head;
    int slotShip[8], slotRem[8];
    int left;
};

static inline int AW() { return W + 2; }
static inline int AI(int x, int y) { return (y + 1) * AW() + (x + 1); }

void floodFrom(State& s, int x, int y) {
    int aw = AW(), ah = H + 2, id = y * aw + x;
    if (s.air[id]) return;
    vector<int> st{id};
    s.air[id] = 1;
    const int dx[4] = {1, -1, 0, 0}, dy[4] = {0, 0, 1, -1};
    while (!st.empty()) {
        int c = st.back(); st.pop_back();
        int cx = c % aw, cy = c / aw;
        for (int d = 0; d < 4; d++) {
            int nx = cx + dx[d], ny = cy + dy[d];
            if (nx < 0 || ny < 0 || nx >= aw || ny >= ah) continue;
            int n = ny * aw + nx;
            if (s.air[n]) continue;
            bool occ = nx >= 1 && ny >= 1 && nx <= W && ny <= H && s.occ[(ny - 1) * W + (nx - 1)] >= 0;
            if (occ) continue;
            s.air[n] = 1; st.push_back(n);
        }
    }
}

State initState() {
    State s;
    s.occ.assign(W * H, -1);
    s.left = 0;
    for (int i = 0; i < W * H; i++) { s.occ[i] = grid0[i]; if (grid0[i] >= 0) s.left++; }
    s.air.assign((W + 2) * (H + 2), 0);
    floodFrom(s, 0, 0);
    s.lanes.assign(COLS, {});
    s.head.assign(COLS, 0);
    for (int i = 0; i < (int)ships.size(); i++) s.lanes[i % COLS].push_back(i);
    for (int i = 0; i < 8; i++) { s.slotShip[i] = -1; s.slotRem[i] = 0; }
    return s;
}

inline bool accessible(const State& s, int x, int y) {
    return s.air[AI(x - 1, y)] || s.air[AI(x + 1, y)] || s.air[AI(x, y - 1)] || s.air[AI(x, y + 1)];
}

void removeCube(State& s, int x, int y) {
    s.occ[y * W + x] = -1;
    s.left--;
    if (accessible(s, x, y)) floodFrom(s, x + 1, y + 1);
}

int pick(const State& s, int k) {
    for (int y = 0; y < H; y++) {
        int mn = INT_MAX, mx = INT_MIN;
        for (int x = 0; x < W; x++) if (s.occ[y * W + x] >= 0) { mn = min(mn, x); mx = max(mx, x); }
        if (mn == INT_MAX) continue;
        int best = -1, bd = INT_MAX, bl = 0, bx = 0;
        for (int x = mn; x <= mx; x++) {
            if (s.occ[y * W + x] != k || !accessible(s, x, y)) continue;
            int d = min(x - mn, mx - x), lft = (x - mn <= mx - x) ? 0 : 1;
            if (best < 0 || d < bd || (d == bd && (lft < bl || (lft == bl && x < bx)))) { best = y * W + x; bd = d; bl = lft; bx = x; }
        }
        if (best >= 0) return best;
    }
    return -1;
}

void finishOne(State& s, int i) {
    int p = partnerCache[s.slotShip[i]];
    if (p < 0) { s.slotShip[i] = -1; return; }
    for (int j = 0; j < SLOTS; j++) if (s.slotShip[j] == p && s.slotRem[j] == 0) { s.slotShip[j] = -1; s.slotShip[i] = -1; return; }
}

int collect(State& s) {
    int total = 0;
    bool prog = true;
    while (prog) {
        prog = false;
        for (int i = 0; i < SLOTS; i++) {
            if (s.slotShip[i] < 0 || s.slotRem[i] == 0) continue;
            int c = pick(s, ships[s.slotShip[i]].color);
            if (c < 0) continue;
            removeCube(s, c % W, c / W);
            total++; prog = true;
            if (--s.slotRem[i] == 0) finishOne(s, i);
        }
    }
    return total;
}

int collectNoisy(State& s, int budget) {
    int total = 0;
    while (budget > 0) {
        int cand[8], nc = 0;
        for (int i = 0; i < SLOTS; i++)
            if (s.slotShip[i] >= 0 && s.slotRem[i] > 0 && pick(s, ships[s.slotShip[i]].color) >= 0) cand[nc++] = i;
        if (!nc) break;
        int i = cand[rng() % nc], trip = 1 + rng() % 5;
        while (trip-- > 0 && budget > 0 && s.slotRem[i] > 0) {
            int c = pick(s, ships[s.slotShip[i]].color);
            if (c < 0) break;
            removeCube(s, c % W, c / W);
            total++; budget--;
            if (--s.slotRem[i] == 0) { finishOne(s, i); break; }
        }
    }
    return total;
}

int emptySlots(const State& s) { int n = 0; for (int i = 0; i < SLOTS; i++) if (s.slotShip[i] < 0) n++; return n; }

vector<int> legal(const State& s) {
    vector<int> a;
    int e = emptySlots(s);
    if (!e) return a;
    for (int c = 0; c < COLS; c++) {
        if (s.head[c] >= (int)s.lanes[c].size()) continue;
        int id = s.lanes[c][s.head[c]], p = partnerCache[id];
        if (p < 0) { a.push_back(c); continue; }
        if (e < 2) continue;
        int pc = p % COLS, pos = -1;
        for (int k = s.head[pc]; k < (int)s.lanes[pc].size(); k++) if (s.lanes[pc][k] == p) { pos = k - s.head[pc]; break; }
        if (pos < 0 || pos >= ROWS) continue;
        bool ok = true;
        for (int k = 0; k < pos; k++) if (s.lanes[pc][s.head[pc] + k] != id) { ok = false; break; }
        if (!ok) continue;
        if ((pc == c && pos == 1) || (pc != c && pos == 0)) a.push_back(c);
    }
    return a;
}

void dock(State& s, int id) {
    for (int i = 0; i < SLOTS; i++) if (s.slotShip[i] < 0) { s.slotShip[i] = id; s.slotRem[i] = ships[id].cap; return; }
}

void sendOnly(State& s, int c) {
    int id = s.lanes[c][s.head[c]], p = partnerCache[id];
    s.head[c]++;
    dock(s, id);
    if (p >= 0) { s.head[p % COLS]++; dock(s, p); }
}
void apply(State& s, int c) { sendOnly(s, c); collect(s); }
void applyReal(State& s, int c) {
    if (!NOISE) { apply(s, c); return; }
    sendOnly(s, c);
    collectNoisy(s, NOISE == 2 ? 30 + rng() % 120 : INT_MAX);
}
vector<int> legalOrWait(State& s) {
    auto a = legal(s);
    if (a.empty() && NOISE) { collectNoisy(s, INT_MAX); a = legal(s); }
    return a;
}

double scoreAfter(const State& s0, int c) {
    State s = s0;
    int before = s.left;
    apply(s, c);
    if (s.left == 0) return 1e9;
    if (legal(s).empty()) return -1e9;
    int idle = 0;
    for (int i = 0; i < SLOTS; i++) if (s.slotShip[i] >= 0) idle++;
    return (before - s.left) * 1.0 - idle * 3.0 + emptySlots(s) * 2.0;
}

int playRandom() {
    State s = initState();
    while (s.left > 0) {
        auto a = legalOrWait(s);
        if (a.empty()) return 0;
        applyReal(s, a[rng() % a.size()]);
    }
    return 1;
}

int smartMove(const State& s, const vector<int>& a) {
    double best = -1e18; int bc = a[0];
    for (int c : a) { double v = scoreAfter(s, c) + (rng() % 1000) * 1e-4; if (v > best) { best = v; bc = c; } }
    return bc;
}

int playSmart() {
    State s = initState();
    while (s.left > 0) {
        auto a = legalOrWait(s);
        if (a.empty()) return 0;
        applyReal(s, smartMove(s, a));
    }
    return 1;
}

long long nodes;
unordered_set<uint64_t> seen;
uint64_t hashState(const State& s) {
    uint64_t h = 1469598103934665603ULL;
    for (int c = 0; c < COLS; c++) { h ^= s.head[c] + 1; h *= 1099511628211ULL; }
    vector<pair<int, int>> v;
    for (int i = 0; i < SLOTS; i++) if (s.slotShip[i] >= 0) v.push_back({s.slotShip[i], s.slotRem[i]});
    sort(v.begin(), v.end());
    for (auto& p : v) { h ^= p.first * 131 + p.second; h *= 1099511628211ULL; }
    return h;
}
int dfs(const State& s) {
    if (s.left == 0) return 1;
    if (++nodes > 400000) return -1;
    uint64_t h = hashState(s);
    if (seen.count(h)) return 0;
    seen.insert(h);
    vector<pair<double, int>> ord;
    for (int c : legal(s)) ord.push_back({-scoreAfter(s, c), c});
    sort(ord.begin(), ord.end());
    int res = 0;
    for (auto& o : ord) {
        if (o.first > 1e8) continue;
        State t = s; apply(t, o.second);
        int r = dfs(t);
        if (r == 1) return 1;
        if (r < 0) res = -1;
    }
    return res;
}
int dfsFrom(const State& s) { nodes = 0; seen.clear(); return dfs(s); }

void ponrReport(int runs) {
    int losses = 0, wins = 0; double gapSum = 0;
    for (int r = 0; r < runs; r++) {
        State s = initState();
        vector<State> hist;
        bool lost = false;
        while (s.left > 0) {
            hist.push_back(s);
            auto a = legal(s);
            if (a.empty()) { lost = true; break; }
            apply(s, smartMove(s, a));
        }
        if (!lost) { wins++; continue; }
        losses++;
        int n = hist.size(), last = -1;
        for (int i = n - 1; i >= 0; i--) if (dfsFrom(hist[i]) != 0) { last = i; break; }
        gapSum += last < 0 ? n : n - 1 - last;
    }
    printf("smart wins %d/%d, losses %d, avg moves from mistake to fail %.1f\n", wins, runs, losses, losses ? gapSum / losses : 0);
}

int main(int argc, char** argv) {
    ifstream f(argv[1]);
    int runs = atoi(argv[2]);
    rng.seed(argc > 3 ? atoi(argv[3]) : 12345);
    f >> W >> H;
    grid0.assign(W * H, -1);
    for (int r = 0; r < H; r++) for (int x = 0; x < W; x++) { int v; f >> v; grid0[(H - 1 - r) * W + x] = v; }
    f >> COLS >> ROWS >> SLOTS;
    int n; f >> n;
    ships.resize(n);
    for (auto& s : ships) f >> s.color >> s.cap >> s.link >> s.hidden;
    partnerCache.assign(n, -1);
    for (int i = 0; i < n; i++) if (ships[i].link)
        for (int j = 0; j < n; j++) if (j != i && ships[j].link == ships[i].link) partnerCache[i] = j;
    if (argc > 5 && argv[5][0] == 'n') NOISE = 1;
    if (argc > 5 && argv[5][0] == 'm') NOISE = 2;
    if (argc > 4 && argv[4][0] == 'p') { ponrReport(runs); return 0; }
    int rw = 0, sw = 0;
    for (int i = 0; i < runs; i++) rw += playRandom();
    for (int i = 0; i < runs; i++) sw += playSmart();
    int d = 2;
    if (!(argc > 4 && argv[4][0] == '0')) d = dfsFrom(initState());
    printf("%.3f %.3f %d\n", rw / (double)runs, sw / (double)runs, d);
}
