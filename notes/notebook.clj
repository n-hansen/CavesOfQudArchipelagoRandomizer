^{:nextjournal.clerk/visibility {:result :hide}}
(ns notebook
  (:require [cheshire.core :as json]
            [clojure.string :as str]
            [babashka.fs :as fs]
            [babashka.process :as p]
            [medley.core :as me]
            [nextjournal.clerk :as clerk]
            [clojure.java.io :as io]))

^{::clerk/visibility #{:hide}}
(defn duckdb [q]
  (let [{:keys [exit err out]} @(p/process {:in q :out :string :err :string}
                                           "duckdb" "--json" "notes/data/data.db")]
    (if-not (zero? exit)
      (throw (ex-info "duckdb error" {:err err}))
      (json/parse-string out true))))

(def sultan-journal-entries
  (let [_run-at #_(java.util.Date.) #inst "2026-10-25T22:16:59.811-00:00" ]
    (duckdb "
WITH history_events AS (
  SELECT
    IFNULL(CAST(gameId AS STRING), filename) AS game_id,
    evt.entity AS entity_name,
    evt.year AS year,
    evt.id AS event_id,
  FROM
    game_data g
    , UNNEST(g.historyEvents) evt
)
, sultan_entries AS (
  SELECT
    IFNULL(CAST(gameId AS STRING), filename) AS game_id,
    ent.sultan AS sultan_id,
    ent.eventId AS event_id,
    LIST_CONTAINS(ent.attributes, 'sultanTombPropaganda') AS is_propaganda
  FROM
    game_data g
    , UNNEST(g.sultanEntries) ent
)

, enriched_entries AS (
  SELECT
    * EXCLUDE (entity_name),
    entity_name AS sultan_name
  FROM
    sultan_entries
    INNER JOIN history_events USING (game_id, event_id)
)

, sultan_ages AS (
  SELECT
    game_id,
    sultan_id,
    ROW_NUMBER() OVER (PARTITION BY game_id ORDER BY first_year) AS age
  FROM (
    SELECT game_id, sultan_id, MIN(year) AS first_year
    FROM enriched_entries
    GROUP BY game_id, sultan_id
  )
)

SELECT *
FROM
  enriched_entries
  LEFT JOIN sultan_ages USING (game_id, sultan_id)
ORDER BY game_id, year
")))

(clerk/vl {:data {:values sultan-journal-entries}
           :transform [{:filter "datum.age < 6 && !datum.is_propaganda"}
                       {:aggregate [{:op :count
                                     :as :event_count}]
                        :groupby [:game_id :age]}]
           :hconcat
           [{:mark {:type :boxplot
                    :extent :min-max}
             :encoding {:x {:field :age
                            :type :ordinal}
                        :y {:field :event_count
                            :type :quantitative
                            :title "per-age event count"}
                        :color {:field :age
                                :type :nominal}}}
            {:mark {:type :boxplot :extent :min-max
                    :color "#666666"}
             :encoding {:y {:field :event_count
                            :type :quantitative
                            :axis {:labels false}
                            :title nil}}}
            {:transform [{:aggregate [{:op :sum
                                       :field :event_count
                                       :as :event_count}]
                          :groupby [:game_id]}]
             :mark {:type :boxplot :extent :min-max
                    :color "#666666"}
             :encoding {:y {:field :event_count
                            :type :quantitative
                            :title "total event count (no resheph)"}}
             }]})

(def most-recent-game-id
  #_(let [_run-at #_(java.util.Date.) #inst "2026-10-25T00:05:15.406-00:00"]
      (-> (duckdb "SELECT gameId FROM game_data WHERE gameId IS NOT NULL ORDER BY filename desc LIMIT 1")
          first :gameId))
  "459adea1-3f7b-4bdf-9199-046ea869fd88")

(def game-events
  (let [_run-at #_(java.util.Date.) #inst "2026-10-25T22:17:26.429-00:00"]
    (-> (format "
WITH history_events AS (
  SELECT
    evt.year AS year,
    evt.id AS event_id
  FROM
    game_data g
    , UNNEST(g.historyEvents) evt
  WHERE gameId = '%s'
)
, sultan_entries AS (
  SELECT
    ent.sultan AS sultan_id,
    ent.eventId AS event_id,
    ent.entryId AS entry_id,
    LIST_CONTAINS(ent.attributes, 'sultanTombPropaganda') AS is_propaganda
  FROM
    game_data g
    , UNNEST(g.sultanEntries) ent
  WHERE gameId = '%s'
)

, enriched_entries AS (
  SELECT *
  FROM
    sultan_entries
    INNER JOIN history_events USING (event_id)
)

, sultan_ages AS (
  SELECT
    sultan_id,
    ROW_NUMBER() OVER (ORDER BY first_year) AS age
  FROM (
    SELECT sultan_id, MIN(year) AS first_year
    FROM enriched_entries
    GROUP BY sultan_id
  )
)

SELECT
  turns,
  timeTicks AS time_ticks,
  entry_id,
  age,
  playerLevel AS level,
  playerXp AS xp
FROM
  game_events evt
  INNER JOIN enriched_entries ent ON evt.journalEntryId = ent.entry_id
  INNER JOIN sultan_ages USING (sultan_id)
ORDER BY turns
"
                most-recent-game-id
                most-recent-game-id)
        (duckdb))))

^{::clerk/visibility #{:hide}}
(def levelup-xp
  (->> game-events
       (map :level)
       (apply max)
       (inc)
       (range 2)
       (map (juxt identity #(+ 100 (* 15 % % %))))))

^{::clerk/visibility #{:hide}}
(defn add-lvl
  [{:as d :keys [xp]}]
  (let [lvl (loop [[[next-lvl breakpoint] & lups] levelup-xp
                   last-breakpoint 0
                   lvl 1]
              (cond
                (nil? next-lvl) (inc lvl)
                (< xp breakpoint) (double (+ lvl (/ (- xp last-breakpoint)
                                                    (- breakpoint last-breakpoint))))
                :else (recur lups
                             breakpoint
                             next-lvl)))]
    (assoc d :lvl lvl)))

(def secret-counts
  (let [{:keys [age-counts events]}
        (->> game-events
             (map add-lvl)
             (reduce (fn [acc {:as evt :keys [age]}]
                       (let [age-count (inc (get-in acc [:age-counts age] 0))
                             ttl-count (inc (get acc :ttl-count 0))]
                         (-> acc
                             (assoc-in [:age-counts age] age-count)
                             (assoc :ttl-count ttl-count)
                             (update :events conj
                                     (assoc evt :age :total :cnt ttl-count)
                                     (assoc evt :cnt age-count)))))
                     {:events []}))
        last-event (peek events)]
    (when (not-empty age-counts)
      (apply conj events (concat
                          (for [[age _] age-counts]
                            {:xp 0 :lvl 0 :age age :cnt 0})
                          (for [[age cnt] age-counts
                                :when (not= (:age last-event) age)]
                            (assoc last-event :age age :cnt cnt)))))))

(clerk/vl {:data {:values secret-counts}
           :mark {:type :line
                  :interpolate :step-after
                  :tooltip true}
           :encoding {:x {:field :lvl
                          :type :quantitative}
                      :y {:field :cnt
                          :type :quantitative}
                      :color {:field :age
                              :type :nominal}}})
