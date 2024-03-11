namespace MovieTitler.Generation

type Movie = {
    Year: int
    Title: string
}

module Movie =
    let GetYear movie = movie.Year

    let FilterByRelevancy movies most_recent_year_max other_years_max = [
        let most_recent_year =
            movies
            |> Seq.map GetYear
            |> Seq.max
        for year, movies in movies |> Seq.groupBy GetYear do
            let count =
                if year = most_recent_year
                then most_recent_year_max
                else other_years_max
            yield! Seq.truncate count movies
    ]
