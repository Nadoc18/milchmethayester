"""
Simulateur d'equilibrage de Milchemet HaYetzer.

Rejoue les regles telles qu'elles sont ecrites dans InteractionRules / PortalManager /
TurnManager, hors Unity, pour repondre a trois questions :

  1. Une partie dure-t-elle 20-30 tours ?
  2. Plusieurs strategies sont-elles viables ?
  3. Aucune strategie n'est-elle degeneree (gagne toujours, sans arbitrage) ?

Le plateau est abstrait : on suit les distances, pas les cases. C'est suffisant pour
l'economie et le rythme, qui sont ce qu'on veut valider.
"""
import random
from dataclasses import dataclass, field

# ---- constantes, recopiees de InteractionRules.cs ----
STARTING_ENERGY = 60
BASE_INCOME = 12
TRIVIA_REWARD = 25
CRYSTAL_INCOME = {1: 5, 2: 10}

TANK_COST = 50
TANK_EVOLVE_COST = 60
COST = {
    ('hill', 1): 40, ('hill', 2): 60,
    ('gas', 1): 30, ('gas', 2): 45,
    ('crystal', 1): 60, ('crystal', 2): 90,
    ('mountain', 1): 50, ('mountain', 2): 75,
}

BASE_HP = 200
PORTAL_HP = {1: 50, 2: 100}
PORTAL_EVOLVE_AFTER = 18
PORTAL_SPAWN_INTERVAL = 3
MAX_SPAWNS_PER_TURN = 3
MAX_ACTIVE_ENEMIES = 16
SURGE_EXTRA = 2
FIRST_SURGE_TURN = 6
SURGE_INTERVAL = 5

KILL_BACKLASH = 12
KILLS_TO_BREAK = 4
SHIELD_DOWN_TURNS = 4
SHIELD_DIVISOR = 2
BACKLASH_FLOOR_PCT = 20

BUNKER = {1: (10, 4, 40), 2: (15, 6, 80)}   # degats, cibles, PV
BUNKER_RANGE = 2
BUNKER_UPKEEP = {1: 3, 2: 5}
GAS_HEAL = {1: 10, 2: 20}
MOUNTAIN_DECAY = 10
MOUNTAIN_HP = {1: 30, 2: 50}

TANK_STATS = {1: (30, 10, 1, 1), 2: (60, 20, 2, 2)}   # PV, degats, portee, mobilite
ENEMY_STATS = {1: (20, 10, 1), 2: (40, 15, 2), 3: (80, 30, 3)}

BOARD_RADIUS = 7
TRIVIA_SUCCESS_RATE = 0.75   # un joueur qui connait ses sources, sans etre parfait


@dataclass
class Tank:
    level: int = 1
    hp: int = 30
    stance: str = 'guard'
    dist_to_base: int = 2
    target_portal: int = -1

    @property
    def dmg(self):
        return TANK_STATS[self.level][1]

    @property
    def move(self):
        return TANK_STATS[self.level][3]

    @property
    def reach(self):
        return TANK_STATS[self.level][2]


@dataclass
class Enemy:
    level: int = 1
    hp: int = 20
    origin: int = 0
    dist_to_base: int = BOARD_RADIUS

    @property
    def dmg(self):
        return ENEMY_STATS[self.level][1]

    @property
    def reach(self):
        return ENEMY_STATS[self.level][2]


@dataclass
class Portal:
    idx: int
    evolve_at: int = PORTAL_EVOLVE_AFTER
    level: int = 1
    hp: int = PORTAL_HP[1]
    kills: int = 0
    shield_down: int = 0
    turns_alive: int = 0
    alive: bool = True


@dataclass
class Building:
    kind: str
    level: int
    hp: int
    dist_to_base: int


@dataclass
class Game:
    strategy: str
    rng: random.Random
    turn: int = 1
    energy: int = STARTING_ENERGY
    base_hp: int = BASE_HP
    tanks: list = field(default_factory=lambda: [Tank() for _ in range(3)])
    enemies: list = field(default_factory=list)
    portals: list = field(default_factory=lambda: [Portal(i, PORTAL_EVOLVE_AFTER + i * 3) for i in range(6)])
    buildings: list = field(default_factory=list)
    surge_idx: int = -1
    last_surge: int = 0
    log: list = field(default_factory=list)

    # ---------------- revenu ----------------
    def income(self):
        gained = BASE_INCOME
        for b in self.buildings:
            if b.kind == 'crystal':
                gained += CRYSTAL_INCOME[b.level]
        if self.rng.random() < TRIVIA_SUCCESS_RATE:
            gained += TRIVIA_REWARD
        self.energy += gained

    # ---------------- depense ----------------
    def spend(self):
        """Chaque strategie est une politique d'achat, rejouee jusqu'a epuisement."""
        for _ in range(12):      # garde-fou : jamais plus de 12 achats par tour
            if not self.buy_one():
                break

    def buy_one(self):
        s = self.strategy
        n_bunkers = sum(1 for b in self.buildings if b.kind == 'hill')
        n_crystals = sum(1 for b in self.buildings if b.kind == 'crystal')
        n_gas = sum(1 for b in self.buildings if b.kind == 'gas')

        def build(kind, dist):
            cost = COST[(kind, 1)]
            if self.energy < cost:
                return False
            self.energy -= cost
            hp = BUNKER[1][2] if kind == 'hill' else (MOUNTAIN_HP[1] if kind == 'mountain' else 40)
            self.buildings.append(Building(kind, 1, hp, dist))
            return True

        def make_tank(stance):
            if self.energy < TANK_COST:
                return False
            self.energy -= TANK_COST
            self.tanks.append(Tank(stance=stance))
            return True

        if s == 'turtle':                      # tout en fortifications
            if n_bunkers < 6:
                return build('hill', 2)
            if n_gas < 2:
                return build('gas', 1)
            return make_tank('guard')

        if s == 'rush':                        # tout en Tanks d'assaut
            return make_tank('assault')

        if s == 'economy':                     # cristaux d'abord, armee ensuite
            if n_crystals < 3:
                return build('crystal', 2)
            if n_bunkers < 2:
                return build('hill', 2)
            return make_tank('assault' if len(self.tanks) % 2 else 'guard')

        if s == 'adaptive':
            if self.turn <= 8:
                if n_bunkers < 3:
                    return build('hill', 2)
                if n_crystals < 2:
                    return build('crystal', 2)
                return False
            n_assault = sum(1 for t in self.tanks if t.stance == 'assault')
            if n_assault < 6:
                return make_tank('assault')
            return make_tank('guard')

        if s == 'balanced':                    # socle defensif, puis offensive
            if n_bunkers < 3:
                return build('hill', 2)
            if n_crystals < 1:
                return build('crystal', 2)
            if n_gas < 1:
                return build('gas', 1)
            return make_tank('assault')

        return False

    # ---------------- phase des Tanks ----------------
    def tanks_phase(self):
        for t in list(self.tanks):
            if t.hp <= 0:
                continue

            if t.stance == 'assault':
                p = self.pick_portal(t)
                if p is None:
                    continue
                # distance abstraite : le Tank part a 2 de la Base, le portail est a 7.
                if t.dist_to_base < BOARD_RADIUS - t.reach:
                    t.dist_to_base = min(BOARD_RADIUS, t.dist_to_base + t.move)
                    continue

                # Legitime defense : un ennemi encore poste pres du portail est
                # abattu avant que le Tank ne frappe la structure. Sans cela le
                # Tank encaissait gratuitement a chaque tour.
                guards = [e for e in self.enemies
                          if e.origin == p.idx and e.hp > 0 and e.dist_to_base >= BOARD_RADIUS - 1]
                dealt_portal = p.hp <= (t.dmg if p.shield_down > 0 else max(1, t.dmg // SHIELD_DIVISOR))

                if guards and not dealt_portal:
                    g = guards[0]
                    g.hp -= t.dmg
                    if g.hp <= 0:
                        self.kill_enemy(g)
                    else:
                        t.hp -= g.dmg
                    continue

                dealt = t.dmg if p.shield_down > 0 else max(1, t.dmg // SHIELD_DIVISOR)
                p.hp -= dealt
                if p.hp <= 0:
                    p.alive = False
                    self.log.append((self.turn, 'portal_down', p.idx))
                if guards:
                    t.hp -= guards[0].dmg
            else:
                # garde / chasse : intercepte l'ennemi le plus avance
                target = self.closest_enemy(guard_only=(t.stance == 'guard'))
                if target is None:
                    continue
                target.hp -= t.dmg
                if target.hp <= 0:
                    self.kill_enemy(target)
                elif target.reach >= 1:
                    t.hp -= target.dmg

        self.tanks = [t for t in self.tanks if t.hp > 0]

    def pick_portal(self, tank):
        alive = [p for p in self.portals if p.alive]
        if not alive:
            return None
        # priorite au bouclier tombe, sinon le premier encore debout
        broken = [p for p in alive if p.shield_down > 0]
        return (broken or alive)[0]

    def closest_enemy(self, guard_only):
        cands = [e for e in self.enemies if e.hp > 0]
        if guard_only:
            cands = [e for e in cands if e.dist_to_base <= 4]
        if not cands:
            return None
        return min(cands, key=lambda e: e.dist_to_base)

    def kill_enemy(self, e):
        p = self.portals[e.origin]
        if p.alive:
            p.kills += 1
            # Le contrecoup use le portail mais ne le ferme jamais : le dernier
            # coup doit venir d'un Tank. Resister affaiblit, attaquer acheve.
            floor = max(1, PORTAL_HP[p.level] * BACKLASH_FLOOR_PCT // 100)
            p.hp = max(floor, p.hp - KILL_BACKLASH)
            if False:
                pass
            elif p.kills >= KILLS_TO_BREAK:
                p.kills = 0
                p.shield_down = SHIELD_DOWN_TURNS
                self.log.append((self.turn, 'shield_broken', p.idx))

    # ---------------- phase ennemie ----------------
    def enemies_phase(self):
        # Un Tank campe devant un portail attire ses gardiens : l'IA cible en
        # priorite ce qui est a portee immediate, pas la Base lointaine.
        besieged = {}
        for t in self.tanks:
            if t.hp > 0 and t.stance == 'assault' and t.dist_to_base >= BOARD_RADIUS - 1:
                besieged.setdefault(self.pick_portal(t).idx if self.pick_portal(t) else -1, []).append(t)

        for e in self.enemies:
            if e.hp <= 0:
                continue

            defenders = besieged.get(e.origin)
            if defenders and e.dist_to_base >= BOARD_RADIUS - 1:
                t = defenders[0]
                t.hp -= e.dmg
                if t.hp <= 0:
                    defenders.pop(0)
                continue

            if e.dist_to_base > e.reach:
                e.dist_to_base -= 1
                continue
            # au contact : frappe la Base, sauf si un batiment le gene
            blockers = [b for b in self.buildings if b.hp > 0 and b.dist_to_base <= 3]
            if blockers and self.rng.random() < 0.35:
                b = blockers[0]
                b.hp -= e.dmg
                if b.hp <= 0:
                    self.buildings.remove(b)
            else:
                self.base_hp -= e.dmg
        self.enemies = [e for e in self.enemies if e.hp > 0]

    # ---------------- fin de tour ----------------
    def buildings_phase(self):
        # Entretien : un Bunker non paye ne tire pas ce tour-ci.
        budget = self.energy
        powered = set()
        for b in self.buildings:
            if b.kind != 'hill':
                continue
            cost = BUNKER_UPKEEP[b.level]
            if budget >= cost:
                budget -= cost
                powered.add(id(b))
        self.energy = budget

        for b in list(self.buildings):
            if b.kind == 'hill' and id(b) not in powered:
                continue
            if b.kind == 'hill':
                dmg, shots, _ = BUNKER[b.level]
                fired = 0
                for e in sorted(self.enemies, key=lambda x: x.dist_to_base):
                    if fired >= shots:
                        break
                    if e.hp <= 0 or e.dist_to_base > b.dist_to_base + BUNKER_RANGE:
                        continue
                    e.hp -= dmg
                    fired += 1
                    if e.hp <= 0:
                        self.kill_enemy(e)
                self.enemies = [e for e in self.enemies if e.hp > 0]
            elif b.kind == 'gas':
                for t in self.tanks:
                    if t.dist_to_base <= b.dist_to_base + 1:
                        t.hp = min(TANK_STATS[t.level][0], t.hp + GAS_HEAL[b.level])
            elif b.kind == 'mountain':
                b.hp -= MOUNTAIN_DECAY
                if b.hp <= 0:
                    self.buildings.remove(b)

    def portals_phase(self):
        spawned = 0
        active = len(self.enemies)
        order = [p for p in self.portals if p.alive]
        self.rng.shuffle(order)

        for p in order:
            p.turns_alive += 1
            if p.shield_down > 0:
                p.shield_down -= 1
                continue
            if p.level < 2 and p.turns_alive >= p.evolve_at:
                damage_taken = PORTAL_HP[1] - p.hp
                p.level = 2
                p.hp = PORTAL_HP[2] - damage_taken

            surging = (p.idx == self.surge_idx)
            wanted = 1 + SURGE_EXTRA if surging else (1 if p.turns_alive % PORTAL_SPAWN_INTERVAL == 0 else 0)

            for _ in range(wanted):
                if spawned >= MAX_SPAWNS_PER_TURN and not surging:
                    break
                if active >= MAX_ACTIVE_ENEMIES:
                    break
                lvl = self.draw_level(p)
                hp = ENEMY_STATS[lvl][0]
                self.enemies.append(Enemy(lvl, hp, p.idx, BOARD_RADIUS))
                spawned += 1
                active += 1

        self.surge_idx = -1
        alive = [p for p in self.portals if p.alive and p.shield_down == 0]
        # Le Yetzer Hara se renforce si on le laisse tranquille : les vagues se
        # rapprochent avec le temps. Rester sur la defensive n'est pas un abri.
        interval = max(2, SURGE_INTERVAL - self.turn // 10)
        if alive and self.turn + 1 >= FIRST_SURGE_TURN and self.turn - self.last_surge >= interval:
            self.surge_idx = self.rng.choice(alive).idx
            self.last_surge = self.turn

    def draw_level(self, p):
        roll = self.rng.random()
        if self.turn < 7:
            lvl = 1
        elif self.turn < 13:
            lvl = 1 if roll < 0.70 else 2
        else:
            lvl = 1 if roll < 0.45 else (2 if roll < 0.85 else 3)
        if p.level >= 2:
            lvl += 1
            if self.rng.random() < 0.10:
                lvl += 1
        return max(1, min(3, lvl))

    # ---------------- boucle ----------------
    def run(self, max_turns=60):
        while self.turn <= max_turns:
            self.income()
            self.spend()
            self.tanks_phase()
            self.enemies_phase()
            self.buildings_phase()
            self.portals_phase()

            if self.base_hp <= 0:
                return ('defeat', self.turn)
            if not any(p.alive for p in self.portals):
                return ('victory', self.turn)
            self.turn += 1
        return ('timeout', max_turns)


def evaluate(strategy, runs=400, seed=1):
    wins = losses = timeouts = 0
    lengths = []
    for i in range(runs):
        g = Game(strategy, random.Random(seed * 100000 + i))
        outcome, turn = g.run()
        if outcome == 'victory':
            wins += 1
            lengths.append(turn)
        elif outcome == 'defeat':
            losses += 1
            lengths.append(turn)
        else:
            timeouts += 1
    avg = sum(lengths) / len(lengths) if lengths else 0
    return {
        'strategy': strategy,
        'win%': 100.0 * wins / runs,
        'loss%': 100.0 * losses / runs,
        'timeout%': 100.0 * timeouts / runs,
        'avg_turns': avg,
    }


if __name__ == '__main__':
    print("%-10s %8s %8s %10s %11s" % ('strategie', 'victoire', 'defaite', 'inachevee', 'duree moy.'))
    print("-" * 52)
    for s in ('turtle', 'rush', 'economy', 'balanced', 'adaptive'):
        r = evaluate(s)
        print("%-10s %7.1f%% %7.1f%% %9.1f%% %10.1f" %
              (r['strategy'], r['win%'], r['loss%'], r['timeout%'], r['avg_turns']))
